using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

using System.Data;

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

        var camposCalculados = await ObterCamposCalculadosAsync(conexao, tabelaOrigem);

        var colunas = schema.Rows
            .Cast<DataRow>()
            .Select(r => new
            {
                Nome = ((string)r["ColumnName"]).Trim(),
                Ordinal = Convert.ToInt32(r["ColumnOrdinal"]),
                SomenteLeitura = r.Table.Columns.Contains("IsReadOnly") &&
                                 r["IsReadOnly"] != DBNull.Value &&
                                 Convert.ToBoolean(r["IsReadOnly"])
            })
            .Where(c => !camposCalculados.Contains(c.Nome) && !c.SomenteLeitura)
            .OrderBy(c => c.Ordinal)
            .Select(c => (c.Ordinal, c.Nome))
            .ToArray();

        if (colunas.Length == 0)
            throw new InvalidOperationException("Nenhuma coluna gravável foi encontrada para exportação.");

        if (camposCalculados.Count > 0)
        {
            Console.WriteLine(
                $"Campos calculados ignorados na exportação: {string.Join(", ", camposCalculados.OrderBy(c => c))}");
        }

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

    private static async Task<HashSet<string>> ObterCamposCalculadosAsync(FbConnection conexao, string tabela)
    {
        const string sql = """
                           SELECT TRIM(rf.RDB$FIELD_NAME) AS CAMPO
                           FROM RDB$RELATION_FIELDS rf
                           JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE
                           WHERE UPPER(TRIM(rf.RDB$RELATION_NAME)) = @TABELA
                             AND f.RDB$COMPUTED_SOURCE IS NOT NULL
                           """;

        var campos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("TABELA", tabela.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            campos.Add(reader.GetString(0).Trim());
        }

        return campos;
    }
}
