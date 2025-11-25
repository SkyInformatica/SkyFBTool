using System.Text;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;

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

        string caminhoLogErros = "erros_importacao.log";
        if (File.Exists(caminhoLogErros))
            File.Delete(caminhoLogErros);

        // --------------------------------------------------------------------
        // 🔵 PRIMEIRO PASSO: LER APENAS AS LINHAS SET NAMES / SET SQL DIALECT
        // --------------------------------------------------------------------

        string? charsetArquivo = null;

        using (var leitorCabecalho = new StreamReader(
            opcoes.ArquivoEntrada,
            new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: true))
        {
            for (int i = 0; i < 5; i++)  // ler somente início do arquivo
            {
                string? l = await leitorCabecalho.ReadLineAsync();
                if (l == null) break;

                string linha = l.TrimStart('\uFEFF', '\u200B', '\u00A0', '\u2060', ' ', '\t')
                                 .Trim();

                if (linha.StartsWith("SET NAMES", StringComparison.OrdinalIgnoreCase))
                {
                    var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length >= 3)
                        charsetArquivo = partes[2].Replace(";", "").Trim().ToUpperInvariant();
                }
            }
        }

        if (charsetArquivo == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Aviso: Arquivo não contém 'SET NAMES'. Usando charset UTF8.");
            Console.ResetColor();
            charsetArquivo = "UTF8";
        }

        Console.WriteLine($"Charset detectado: {charsetArquivo}\n");

        // --------------------------------------------------------------------
        // 🔵 ABRIR CONEXÃO COM O CHARSET CORRETO
        // --------------------------------------------------------------------

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

        FbTransaction? transacao = null;

        long totalLinhas = 0;

        // --------------------------------------------------------------------
        // 🔵 SEGUNDO PASSO: LEITURA REAL DO ARQUIVO
        // --------------------------------------------------------------------

        using var leitor = new StreamReader(
            opcoes.ArquivoEntrada,
            new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: true);

        string? linhaOriginal;
        while ((linhaOriginal = await leitor.ReadLineAsync()) != null)
        {
            totalLinhas++;

            // Sanitização leve
            string linha = linhaOriginal.TrimStart('\uFEFF', '\u200B', '\u00A0', '\u2060', ' ', '\t');

            // Ignorar comando SET SQL DIALECT
            if (linha.StartsWith("SET SQL DIALECT", StringComparison.OrdinalIgnoreCase))
                continue;

            // Ignorar o SET NAMES porque já aplicamos no início
            if (linha.StartsWith("SET NAMES", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                transacao = await ExecutorSql.ExecutarAsync(
                    linha,
                    conexao,
                    transacao,
                    opcoes,
                    caminhoLogErros
                );
            }
            catch (Exception ex)
            {
                if (!opcoes.ContinuarEmCasoDeErro)
                    throw;

                File.AppendAllText(caminhoLogErros,
                    $"Erro na linha {totalLinhas}: {ex.Message}{Environment.NewLine}");
            }

            if (opcoes.ProgressoACada > 0 &&
                totalLinhas % opcoes.ProgressoACada == 0)
            {
                Console.WriteLine($"Linhas processadas: {totalLinhas:N0}");
            }
        }

        // Commit final
        if (transacao != null)
        {
            await transacao.CommitAsync();
            await transacao.DisposeAsync();
        }

        Console.WriteLine("\nImportação concluída com sucesso.");
        Console.WriteLine($"Total de linhas processadas: {totalLinhas:N0}");

        if (opcoes.ContinuarEmCasoDeErro && File.Exists(caminhoLogErros))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Atenção: ocorreram erros durante a importação.");
            Console.WriteLine($"Consulte o arquivo: {caminhoLogErros}");
            Console.ResetColor();
        }
    }
}
