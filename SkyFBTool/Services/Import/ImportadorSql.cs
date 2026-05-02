using System.Diagnostics;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Import;

public static class ImportadorSql
{
    private const long CheckpointUnidades = 50_000;
    private static readonly TimeSpan IntervaloCheckpoint = TimeSpan.FromSeconds(30);

    public static async Task<ResultadoImportacaoSql> ImportarAsync(OpcoesImportacao opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.ArquivoEntrada))
            throw new ArgumentException("Arquivo SQL nÃ£o informado (--input).");

        if (!File.Exists(opcoes.ArquivoEntrada))
            throw new FileNotFoundException($"Arquivo SQL nÃ£o encontrado: {opcoes.ArquivoEntrada}");

        Console.WriteLine($"Iniciando importaÃ§Ã£o do arquivo '{opcoes.ArquivoEntrada}'...\n");

        var inicioExecucao = DateTime.UtcNow;
        string caminhoLog = ResolverCaminhoLogImportacao(opcoes.ArquivoEntrada);
        await File.WriteAllTextAsync(
            caminhoLog,
            $"Log de importaÃ§Ã£o{Environment.NewLine}" +
            $"Arquivo SQL: {opcoes.ArquivoEntrada}{Environment.NewLine}" +
            $"InÃ­cio (UTC): {inicioExecucao:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{Environment.NewLine}");
        Console.WriteLine($"Log da importaÃ§Ã£o: {caminhoLog}");

        string charsetArquivo = CharsetSql.DetectarCharsetSetNames(opcoes.ArquivoEntrada);
        Encoding encodingArquivo = CharsetSql.ResolverEncodingLeituraSql(charsetArquivo);

        Console.WriteLine($"Charset detectado para conexÃ£o: {charsetArquivo}\n");

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

        long totalLinhasLidas = 0;
        long totalLinhasProcessadas = 0;
        long totalComandos = 0;
        long totalErros = 0;
        long comandosDesdeUltimoBatch = 0;

        var cronometroTotal = Stopwatch.StartNew();
        var cronometroVelocidade = Stopwatch.StartNew();
        var ultimoCheckpointEm = DateTime.UtcNow;
        bool modoDinamico = !Console.IsOutputRedirected;
        long comandosUltimaMedicao = 0;
        long unidadesUltimoCheckpoint = 0;

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
            totalLinhasLidas++;
            dentroComentarioLinha = false;

            string linhaAnalise = linhaOriginal.TrimStart('\uFEFF', '\u200B', '\u00A0', '\u2060', ' ', '\t');

            if (string.IsNullOrWhiteSpace(linhaAnalise) && comandoAtual.Length == 0)
            {
                AtualizarProgresso(
                    totalLinhasProcessadas,
                    totalComandos,
                    ref comandosUltimaMedicao,
                    ref unidadesUltimoCheckpoint,
                    ref ultimoCheckpointEm,
                    cronometroTotal,
                    cronometroVelocidade,
                    opcoes,
                    modoDinamico);
                continue;
            }

            if (linhaAnalise.StartsWith("SET SQL DIALECT", StringComparison.OrdinalIgnoreCase)
                && comandoAtual.Length == 0)
            {
                AtualizarProgresso(
                    totalLinhasProcessadas,
                    totalComandos,
                    ref comandosUltimaMedicao,
                    ref unidadesUltimoCheckpoint,
                    ref ultimoCheckpointEm,
                    cronometroTotal,
                    cronometroVelocidade,
                    opcoes,
                    modoDinamico);
                continue;
            }

            if (linhaAnalise.StartsWith("SET NAMES", StringComparison.OrdinalIgnoreCase)
                && comandoAtual.Length == 0)
            {
                AtualizarProgresso(
                    totalLinhasProcessadas,
                    totalComandos,
                    ref comandosUltimaMedicao,
                    ref unidadesUltimoCheckpoint,
                    ref ultimoCheckpointEm,
                    cronometroTotal,
                    cronometroVelocidade,
                    opcoes,
                    modoDinamico);
                continue;
            }

            if (TentarProcessarSetTerm(linhaAnalise, ref delimitadorAtual) && comandoAtual.Length == 0)
            {
                AtualizarProgresso(
                    totalLinhasProcessadas,
                    totalComandos,
                    ref comandosUltimaMedicao,
                    ref unidadesUltimoCheckpoint,
                    ref ultimoCheckpointEm,
                    cronometroTotal,
                    cronometroVelocidade,
                    opcoes,
                    modoDinamico);
                continue;
            }

            totalLinhasProcessadas++;

            if (comandoAtual.Length == 0)
                linhaInicioComando = totalLinhasLidas;

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

                        dentroString = false;
                        i++;
                        continue;
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
                        comandosUltimaMedicao++;

                        string? tabela = DetectarTabela.Extrair(comandoCompleto);
                        if (tabela != null)
                            await controleIndices.DesativarIndicesAsync(tabela, transacao);

                        try
                        {
                            var resultadoExecucao = await ExecutorSql.ExecutarAsync(
                                comandoCompleto,
                                conexao,
                                transacao,
                                opcoes,
                                caminhoLog);
                            transacao = resultadoExecucao.Transacao!;
                            if (resultadoExecucao.HouveErro)
                                totalErros++;
                        }
                        catch (Exception ex)
                        {
                            totalErros++;
                            if (!opcoes.ContinuarEmCasoDeErro)
                            {
                                throw new FalhaImportacaoSqlException(
                                    opcoes.ArquivoEntrada,
                                    linhaInicioComando,
                                    comandoCompleto,
                                    ex);
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
                    linhaInicioComando = totalLinhasLidas;
                    i++;
                    continue;
                }

                comandoAtual.Append(c);
                i++;
            }

            if (!dentroComentarioBloco && !dentroComentarioLinha)
                comandoAtual.AppendLine();

            AtualizarProgresso(
                totalLinhasProcessadas,
                totalComandos,
                ref comandosUltimaMedicao,
                ref unidadesUltimoCheckpoint,
                ref ultimoCheckpointEm,
                cronometroTotal,
                cronometroVelocidade,
                opcoes,
                modoDinamico);
        }

        if (comandoAtual.Length > 0)
        {
            string comandoFinal = comandoAtual.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(comandoFinal))
            {
                totalComandos++;
                comandosDesdeUltimoBatch++;
                comandosUltimaMedicao++;

                try
                {
                    var resultadoExecucao = await ExecutorSql.ExecutarAsync(
                        comandoFinal,
                        conexao,
                        transacao,
                        opcoes,
                        caminhoLog);
                    transacao = resultadoExecucao.Transacao!;
                    if (resultadoExecucao.HouveErro)
                        totalErros++;
                }
                catch (Exception ex)
                {
                    totalErros++;
                    if (!opcoes.ContinuarEmCasoDeErro)
                    {
                        throw new FalhaImportacaoSqlException(
                            opcoes.ArquivoEntrada,
                            linhaInicioComando,
                            comandoFinal,
                            ex);
                    }
                }
            }
        }

        await controleIndices.ReativarTodosAsync(transacao);

        await transacao.CommitAsync();
        await transacao.DisposeAsync();

        if (modoDinamico && opcoes.ProgressoACada > 0)
            Console.WriteLine();

        var fimExecucao = DateTime.UtcNow;
        var duracao = fimExecucao - inicioExecucao;

        Console.WriteLine();
        Console.WriteLine("ImportaÃ§Ã£o concluÃ­da.");
        Console.WriteLine($"Total de linhas processadas : {totalLinhasProcessadas:N0}");
        Console.WriteLine($"Total de comandos executados: {totalComandos:N0}");
        Console.WriteLine($"Tempo total de execuÃ§Ã£o     : {FormatarDuracao(duracao)}");

        double cps = totalComandos / duracao.TotalSeconds;
        Console.WriteLine($"Velocidade mÃ©dia            : {cps:N2} comandos/segundo");

        if (totalErros > 0)
        {
            await File.AppendAllTextAsync(
                caminhoLog,
                $"ImportaÃ§Ã£o concluÃ­da com erros.{Environment.NewLine}" +
                $"Fim (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                $"Total de erros: {totalErros}{Environment.NewLine}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nAviso: ocorreram erros durante a importaÃ§Ã£o.");
            Console.WriteLine($"Consulte o arquivo: {caminhoLog}");
            Console.ResetColor();
        }
        else
        {
            await File.AppendAllTextAsync(
                caminhoLog,
                $"ImportaÃ§Ã£o concluÃ­da sem erros.{Environment.NewLine}" +
                $"Fim (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
        }

        return new ResultadoImportacaoSql
        {
            Arquivo = opcoes.ArquivoEntrada,
            TotalLinhasProcessadas = totalLinhasProcessadas,
            TotalComandosExecutados = totalComandos,
            TotalErros = totalErros
        };
    }

    private static void AtualizarProgresso(
        long linhas,
        long comandos,
        ref long comandosUltimaMedicao,
        ref long unidadesUltimoCheckpoint,
        ref DateTime ultimoCheckpointEm,
        Stopwatch cronometroTotal,
        Stopwatch cronometroVelocidade,
        OpcoesImportacao opcoes,
        bool modoDinamico)
    {
        if (opcoes.ProgressoACada <= 0)
            return;

        if (linhas % opcoes.ProgressoACada == 0)
        {
            double segundos = cronometroVelocidade.Elapsed.TotalSeconds;
            double cps = segundos > 0 ? comandosUltimaMedicao / segundos : 0;

            string linha = $"Processado: {linhas:N0} | Comandos: {comandos:N0} | Velocidade: {cps:N0} cmd/s | Tempo: {FormatarDuracao(cronometroTotal.Elapsed)}";
            if (modoDinamico)
                Console.Write($"\r{linha}");
            else
                Console.WriteLine(linha);

            comandosUltimaMedicao = 0;
            cronometroVelocidade.Restart();
        }

        bool checkpointPorUnidade = (linhas - unidadesUltimoCheckpoint) >= CheckpointUnidades;
        bool checkpointPorTempo = DateTime.UtcNow - ultimoCheckpointEm >= IntervaloCheckpoint;
        if (!checkpointPorUnidade && !checkpointPorTempo)
            return;

        double cpsCheckpoint = cronometroTotal.Elapsed.TotalSeconds > 0
            ? comandos / cronometroTotal.Elapsed.TotalSeconds
            : 0;

        if (modoDinamico)
            Console.WriteLine();

        Console.WriteLine(
            $"Checkpoint: {linhas:N0} linhas | {comandos:N0} comandos | {FormatarDuracao(cronometroTotal.Elapsed)} | {cpsCheckpoint:N0} cmd/s");

        unidadesUltimoCheckpoint = linhas;
        ultimoCheckpointEm = DateTime.UtcNow;
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

    private static string ResolverCaminhoLogImportacao(string arquivoEntrada)
    {
        string nomeBaseEntrada = Path.GetFileNameWithoutExtension(arquivoEntrada);
        if (string.IsNullOrWhiteSpace(nomeBaseEntrada))
            nomeBaseEntrada = "import";

        foreach (char invalido in Path.GetInvalidFileNameChars())
            nomeBaseEntrada = nomeBaseEntrada.Replace(invalido, '_');

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string candidato = Path.Combine(
            Directory.GetCurrentDirectory(),
            $"{nomeBaseEntrada}_import_log_{timestamp}.log");

        int sequencial = 1;
        while (File.Exists(candidato))
        {
            candidato = Path.Combine(
                Directory.GetCurrentDirectory(),
                $"{nomeBaseEntrada}_import_log_{timestamp}_{sequencial:000}.log");
            sequencial++;
        }

        return candidato;
    }
}

public sealed class ResultadoImportacaoSql
{
    public string Arquivo { get; set; } = string.Empty;
    public long TotalLinhasProcessadas { get; set; }
    public long TotalComandosExecutados { get; set; }
    public long TotalErros { get; set; }
    public bool HouveErros => TotalErros > 0;
}
