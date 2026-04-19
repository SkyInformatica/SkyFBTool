using System.Diagnostics;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Import;

public static class ImportadorSql
{
    public static async Task ImportarAsync(OpcoesImportacao opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.ArquivoEntrada))
            throw new ArgumentException("Arquivo SQL não informado (--input).");

        if (!File.Exists(opcoes.ArquivoEntrada))
            throw new FileNotFoundException($"Arquivo SQL não encontrado: {opcoes.ArquivoEntrada}");

        Console.WriteLine($"Iniciando importação do arquivo '{opcoes.ArquivoEntrada}'...\n");

        var inicioExecucao = DateTime.UtcNow;

        string caminhoLogErros = "erros_importacao.log";
        if (File.Exists(caminhoLogErros))
            File.Delete(caminhoLogErros);

        string charsetArquivo = CharsetSql.DetectarCharsetSetNames(opcoes.ArquivoEntrada);
        Encoding encodingArquivo = CharsetSql.ResolverEncodingLeituraSql(charsetArquivo);

        Console.WriteLine($"Charset detectado para conexão: {charsetArquivo}\n");

        var csb = new FbConnectionStringBuilder
        {
            DataSource = opcoes.Host,
            Port = opcoes.Porta,
            Database = opcoes.Database,
            UserID = opcoes.Usuario,
            Password = opcoes.Senha,
            Charset = charsetArquivo,
            Dialect = 3
        };

        await using var conexao = new FbConnection(csb.ConnectionString);
        await conexao.OpenAsync();

        FbTransaction transacao = conexao.BeginTransaction();

        var controleIndices = new ControleIndicesFirebird(conexao);

        long totalLinhas = 0;
        long totalComandos = 0;
        long comandosDesdeUltimoBatch = 0;

        var cronometroVelocidade = Stopwatch.StartNew();
        long comandosDesdeUltimaMedicao = 0;

        char delimitadorAtual = ';';
        var comandoAtual = new StringBuilder();

        bool dentroString = false;
        bool dentroComentarioBloco = false;
        bool dentroComentarioLinha = false;

        long linhaInicioComando = 1;

        using var leitor = new StreamReader(
            opcoes.ArquivoEntrada,
            encodingArquivo,
            detectEncodingFromByteOrderMarks: true);

        string? linhaOriginal;

        while ((linhaOriginal = await leitor.ReadLineAsync()) != null)
        {
            totalLinhas++;
            dentroComentarioLinha = false;

            string linhaAnalise = linhaOriginal.TrimStart('\uFEFF', '\u200B', '\u00A0', '\u2060', ' ', '\t');

            if (string.IsNullOrWhiteSpace(linhaAnalise) && comandoAtual.Length == 0)
            {
                MostrarProgresso(totalLinhas, totalComandos, comandosDesdeUltimaMedicao, cronometroVelocidade, opcoes);
                continue;
            }

            if (linhaAnalise.StartsWith("SET SQL DIALECT", StringComparison.OrdinalIgnoreCase)
                && comandoAtual.Length == 0)
            {
                MostrarProgresso(totalLinhas, totalComandos, comandosDesdeUltimaMedicao, cronometroVelocidade, opcoes);
                continue;
            }

            if (linhaAnalise.StartsWith("SET NAMES", StringComparison.OrdinalIgnoreCase)
                && comandoAtual.Length == 0)
            {
                MostrarProgresso(totalLinhas, totalComandos, comandosDesdeUltimaMedicao, cronometroVelocidade, opcoes);
                continue;
            }

            if (TentarProcessarSetTerm(linhaAnalise, ref delimitadorAtual) && comandoAtual.Length == 0)
            {
                MostrarProgresso(totalLinhas, totalComandos, comandosDesdeUltimaMedicao, cronometroVelocidade, opcoes);
                continue;
            }

            if (comandoAtual.Length == 0)
                linhaInicioComando = totalLinhas;

            string linha = linhaOriginal;
            int i = 0;

            while (i < linha.Length)
            {
                char c = linha[i];

                if (dentroComentarioBloco)
                {
                    if (c == '*' && i + 1 < linha.Length && linha[i + 1] == '/')
                    {
                        dentroComentarioBloco = false;
                        i += 2;
                        continue;
                    }
                    i++;
                    continue;
                }

                if (dentroString)
                {
                    comandoAtual.Append(c);

                    if (c == '\'')
                    {
                        if (i + 1 < linha.Length && linha[i + 1] == '\'')
                        {
                            comandoAtual.Append(linha[i + 1]);
                            i += 2;
                            continue;
                        }
                        else
                        {
                            dentroString = false;
                            i++;
                            continue;
                        }
                    }

                    i++;
                    continue;
                }

                if (c == '-' && i + 1 < linha.Length && linha[i + 1] == '-')
                {
                    dentroComentarioLinha = true;
                    break;
                }

                if (c == '/' && i + 1 < linha.Length && linha[i + 1] == '*')
                {
                    dentroComentarioBloco = true;
                    i += 2;
                    continue;
                }

                if (c == '\'')
                {
                    dentroString = true;
                    comandoAtual.Append(c);
                    i++;
                    continue;
                }

                if (c == delimitadorAtual)
                {
                    string comandoCompleto = comandoAtual.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(comandoCompleto))
                    {
                        totalComandos++;
                        comandosDesdeUltimoBatch++;
                        comandosDesdeUltimaMedicao++;

                        string? tabela = DetectarTabela.Extrair(comandoCompleto);
                        if (tabela != null)
                            await controleIndices.DesativarIndicesAsync(tabela, transacao);

                        try
                        {
                            transacao = (await ExecutorSql.ExecutarAsync(
                                comandoCompleto,
                                conexao,
                                transacao,
                                opcoes,
                                caminhoLogErros))!;
                        }
                        catch
                        {
                            if (!opcoes.ContinuarEmCasoDeErro)
                            {
                                Console.WriteLine();
                                Console.WriteLine($"Erro no comando iniciado na linha {linhaInicioComando}.");
                                throw;
                            }
                        }

                        if (opcoes.ProgressoACada > 0 &&
                            comandosDesdeUltimoBatch >= opcoes.ProgressoACada)
                        {
                            await transacao.CommitAsync();
                            transacao = conexao.BeginTransaction();
                            comandosDesdeUltimoBatch = 0;
                        }
                    }

                    comandoAtual.Clear();
                    linhaInicioComando = totalLinhas;

                    i++;
                    continue;
                }

                comandoAtual.Append(c);
                i++;
            }

            if (!dentroComentarioBloco && !dentroComentarioLinha)
                comandoAtual.AppendLine();

            MostrarProgresso(totalLinhas, totalComandos, comandosDesdeUltimaMedicao, cronometroVelocidade, opcoes);
        }

        if (comandoAtual.Length > 0)
        {
            string comandoFinal = comandoAtual.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(comandoFinal))
            {
                totalComandos++;
                comandosDesdeUltimoBatch++;
                comandosDesdeUltimaMedicao++;

                try
                {
                    transacao = (await ExecutorSql.ExecutarAsync(
                        comandoFinal,
                        conexao,
                        transacao,
                        opcoes,
                        caminhoLogErros))!;
                }
                catch
                {
                    if (!opcoes.ContinuarEmCasoDeErro)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Erro no comando final iniciado na linha {linhaInicioComando}.");
                        throw;
                    }
                }
            }
        }

        await controleIndices.ReativarTodosAsync(transacao);

        await transacao.CommitAsync();
        await transacao.DisposeAsync();

        var fimExecucao = DateTime.UtcNow;
        var duracao = fimExecucao - inicioExecucao;

        Console.WriteLine();
        Console.WriteLine("Importação concluída.");
        Console.WriteLine($"Total de linhas processadas : {totalLinhas:N0}");
        Console.WriteLine($"Total de comandos executados: {totalComandos:N0}");
        Console.WriteLine($"Tempo total de execução     : {FormatarDuracao(duracao)}");

        double cps = totalComandos / duracao.TotalSeconds;
        Console.WriteLine($"Velocidade média            : {cps:N2} comandos/segundo");

        if (opcoes.ContinuarEmCasoDeErro && File.Exists(caminhoLogErros))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nAviso: ocorreram erros durante a importação.");
            Console.WriteLine($"Consulte o arquivo: {caminhoLogErros}");
            Console.ResetColor();
        }
    }

    private static void MostrarProgresso(
        long linhas,
        long comandos,
        long comandosDesdeUltimaMedicao,
        Stopwatch cronometroVelocidade,
        OpcoesImportacao opcoes)
    {
        if (opcoes.ProgressoACada <= 0)
            return;

        if (linhas % opcoes.ProgressoACada == 0)
        {
            double segundos = cronometroVelocidade.Elapsed.TotalSeconds;

            double cps = segundos > 0
                ? comandosDesdeUltimaMedicao / segundos
                : 0;

            Console.Write(
                $"\rLinhas: {linhas:N0} | Comandos: {comandos:N0} | Velocidade: {cps:N0} cmd/s");

            cronometroVelocidade.Restart();
        }
    }

    private static bool TentarProcessarSetTerm(string linha, ref char delimitadorAtual)
    {
        if (!linha.StartsWith("SET TERM", StringComparison.OrdinalIgnoreCase))
            return false;

        var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length < 3)
            return false;

        string token = partes[2].Replace(";", "").Trim();
        if (!string.IsNullOrWhiteSpace(token))
            delimitadorAtual = token[0];

        Console.WriteLine($"[Parser] Delimitador atualizado para '{delimitadorAtual}'.");
        return true;
    }

    private static string FormatarDuracao(TimeSpan tempo)
    {
        return $"{(int)tempo.TotalHours:00}:{tempo.Minutes:00}:{tempo.Seconds:00}.{tempo.Milliseconds:000}";
    }
}
