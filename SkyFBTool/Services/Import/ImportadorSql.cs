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

        Console.WriteLine($"Iniciando importação do arquivo '{opcoes.ArquivoEntrada}'...");

        string caminhoLogErros = "erros_importacao.log";
        if (File.Exists(caminhoLogErros))
            File.Delete(caminhoLogErros);

        //
        // 🔵 ABRIR CONEXÃO COM O FIREBIRD
        //
        var csb = new FbConnectionStringBuilder
        {
            DataSource = opcoes.Host,
            Port = opcoes.Porta,
            Database = opcoes.Database,
            UserID = opcoes.Usuario,
            Password = opcoes.Senha,
            Charset = "UTF8", // será substituído pelo próprio SET NAMES no arquivo
            Dialect = 3
        };

        await using var conexao = new FbConnection(csb.ConnectionString);
        await conexao.OpenAsync();

        // Transação inicial
        FbTransaction? transacao = null;

        long totalLinhas = 0;

        //
        // 🔵 LEITURA DO ARQUIVO (STREAMING)
        //
        using var leitor = new StreamReader(
            opcoes.ArquivoEntrada,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true
        );

        string? linha;
        while ((linha = await leitor.ReadLineAsync()) != null)
        {
            totalLinhas++;

            if (totalLinhas == 1)
            {
                linha = new string(linha
                    .SkipWhile(c =>
                            c == '\uFEFF' || // BOM
                            c == '\u200B' || // ZERO WIDTH SPACE
                            c == '\u00A0' || // NO BREAK SPACE
                            c == '\u2060' || // WORD JOINER
                            c == ' '      || // space
                            c == '\t'        // tab
                    ).ToArray());
            }
            
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

            //
            // 🔵 PROGRESSO
            //
            if (opcoes.ProgressoACada > 0 &&
                totalLinhas % opcoes.ProgressoACada == 0)
            {
                Console.WriteLine($"Linhas processadas: {totalLinhas:N0}");
            }
        }

        //
        // 🔵 COMMIT FINAL (caso o arquivo não tenha colocado um)
        //
        if (transacao != null)
        {
            await transacao.CommitAsync();
            transacao.Dispose();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Importação concluída com sucesso.");
        Console.ResetColor();

        Console.WriteLine($"Total de linhas processadas: {totalLinhas:N0}");

        if (opcoes.ContinuarEmCasoDeErro && File.Exists(caminhoLogErros))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Atenção: ocorreram erros durante a importação.");
            Console.WriteLine($"Consulte o arquivo: {caminhoLogErros}");
            Console.ResetColor();
        }
    }
}
