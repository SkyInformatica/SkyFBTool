using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Export;

public static class ExportadorTabelaFirebird
{
    public static async Task ExportarAsync(OpcoesExportacao opcoes, IDestinoArquivo destino)
    {
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(opcoes.Database))
            throw new ArgumentException("Banco de dados não informado (--database).");

        if (string.IsNullOrWhiteSpace(opcoes.Tabela))
            throw new ArgumentException("Tabela não informada (--table).");

        var tabelaOrigem = opcoes.Tabela;
        var tabelaDestino = string.IsNullOrWhiteSpace(opcoes.AliasTabela)
            ? tabelaOrigem
            : opcoes.AliasTabela;

        Console.WriteLine($"Iniciando exportação da tabela '{tabelaOrigem}' para '{tabelaDestino}'...");
        
        await destino.EscreverLinhaAsync($"SET SQL DIALECT 3;");

        if (opcoes.Charset == null && opcoes.ForcarWin1252)
        {
            await destino.EscreverLinhaAsync($"SET NAMES WIN1252;");
        }
        else
        {
            await destino.EscreverLinhaAsync($"SET NAMES {opcoes.Charset};");
        }

        await destino.EscreverLinhaAsync(string.Empty);

        using var conexao = FabricaConexaoFirebird.CriarConexao(opcoes);
        await conexao.OpenAsync();

        var sqlSelect = ConstrutorConsultaFirebird.MontarSelect(opcoes);

        await using var cmd = new FbCommand(sqlSelect, conexao)
        {
            CommandTimeout = 0
        };

        await using var leitor = await cmd.ExecuteReaderAsync();

        var schema = leitor.GetSchemaTable()
                     ?? throw new InvalidOperationException("Não foi possível obter o schema da tabela.");

        var colunas = schema.Rows
            .Cast<System.Data.DataRow>()
            .Select(r => (string)r["ColumnName"])
            .ToArray();

        long totalLinhas = 0;

        while (await leitor.ReadAsync())
        {
            totalLinhas++;

            string insert = ConstrutorInsert.MontarInsert(
                leitor,
                tabelaDestino,
                colunas,
                opcoes.FormatoBlob,
                opcoes.ForcarWin1252,
                opcoes.SanitizarTexto,
                opcoes.EscaparQuebrasDeLinha
            );

            try
            {
                await destino.EscreverLinhaAsync(insert);
            }
            catch (Exception ex)
            {
                if (!opcoes.ContinuarEmCasoDeErro)
                    throw;

                File.AppendAllText("erros_exportacao.log",
                    $"Erro ao escrever linha {totalLinhas}: {ex.Message}{Environment.NewLine}");
            }

            if (opcoes.CommitACada > 0 &&
                totalLinhas % opcoes.CommitACada == 0)
            {
                await destino.EscreverLinhaAsync("COMMIT;");
            }

            if (opcoes.ProgressoACada > 0 &&
                totalLinhas % opcoes.ProgressoACada == 0)
            {
                Console.WriteLine($"Linhas exportadas: {totalLinhas:N0}");
            }
        }

// Commit final — se o importador não tiver COMMIT próprio no fim, este garante.
        if (totalLinhas > 0 && opcoes.CommitACada > 0)
        {
            await destino.EscreverLinhaAsync("COMMIT;");
        }

        cronometro.Stop();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Exportação concluída com sucesso.");
        Console.ResetColor();

        Console.WriteLine($"Linhas exportadas: {totalLinhas:N0}");
        Console.WriteLine($"Tempo total:       {cronometro.Elapsed:hh\\:mm\\:ss\\.fff}");

    }
}
