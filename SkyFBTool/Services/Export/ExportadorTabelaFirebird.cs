using System.Data;
using System.Diagnostics;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Export;

public static class ExportadorTabelaFirebird
{
    public static async Task ExportarAsync(OpcoesExportacao opcoes, IDestinoArquivo destino)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Tabela))
            throw new ArgumentException("Nome da tabela não informado (--table).");

        if (string.IsNullOrWhiteSpace(opcoes.Database))
            throw new ArgumentException("Caminho do banco não informado (--database).");

        string consultaSql = ConstrutorConsultaFirebird.MontarSelect(
            opcoes.Tabela,
            opcoes.CondicaoWhere
        );

        // Abrir conexão
        await using var conexao = FabricaConexaoFirebird.CriarConexao(opcoes);
        await conexao.OpenAsync();

        await using var comando = new FbCommand
        {
            Connection = conexao,
            CommandText = consultaSql,
            CommandTimeout = 0
        };

        await using var leitor = await comando.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

        int quantidadeColunas = leitor.FieldCount;
        var nomesColunas = new string[quantidadeColunas];

        for (int i = 0; i < quantidadeColunas; i++)
            nomesColunas[i] = leitor.GetName(i);

        long totalLinhas = 0;
        var cronometro = Stopwatch.StartNew();

        string arquivoLog = "erros_exportacao.log";
        if (File.Exists(arquivoLog))
            File.Delete(arquivoLog);

        Console.WriteLine($"Iniciando exportação da tabela '{opcoes.Tabela}'...");

        //
        // 🔥 Cabeçalho SQL do arquivo exportado
        //
        await destino.EscreverLinhaAsync("SET SQL DIALECT 3;");

        if (!string.IsNullOrWhiteSpace(opcoes.Charset))
        {
            await destino.EscreverLinhaAsync($"SET NAMES {opcoes.Charset.ToUpperInvariant()};");
        }
        await destino.EscreverLinhaAsync("");

        string nomeParaInsert = 
            !string.IsNullOrWhiteSpace(opcoes.AliasTabela)
                ? opcoes.AliasTabela
                : opcoes.Tabela;

        while (await leitor.ReadAsync())
        {
            totalLinhas++;

            try
            {
                string linhaInsert = ConstrutorInsert.MontarInsert(
                    leitor,
                    nomeParaInsert,
                    nomesColunas,
                    opcoes.FormatoBlob,
                    opcoes.ForcarWin1252,
                    opcoes.SanitizarTexto,
                    opcoes.EscaparQuebrasDeLinha
                );

                await destino.EscreverLinhaAsync(linhaInsert);
                
                // COMMIT periódico no arquivo SQL
                if (opcoes.CommitACada > 0 && totalLinhas % opcoes.CommitACada == 0)
                {
                    await destino.EscreverLinhaAsync("COMMIT;");
                }
            }
            catch (Exception ex)
            {
                if (!opcoes.ContinuarEmCasoDeErro)
                    throw;

                File.AppendAllText(arquivoLog,
                    $"[Linha {totalLinhas}] Erro: {ex.Message}{Environment.NewLine}");

                continue;
            }

            //
            // 📊 Progresso no console
            //
            if (opcoes.ProgressoACada > 0 &&
                totalLinhas % opcoes.ProgressoACada == 0)
            {
                Console.WriteLine($"Linhas exportadas: {totalLinhas:N0}");
            }
        }

        //
        // COMMIT final para fechar última transação lógica
        //
        await destino.EscreverLinhaAsync("COMMIT;");

        cronometro.Stop();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Exportação concluída com sucesso.");
        Console.ResetColor();

        Console.WriteLine($"Tabela:           {opcoes.Tabela}");
        Console.WriteLine($"Total de linhas:  {totalLinhas:N0}");
        Console.WriteLine($"Tempo total:      {cronometro.Elapsed:hh\\:mm\\:ss\\.fff}");
        Console.WriteLine($"Arquivo de saída: {opcoes.ArquivoSaida}");

        if (opcoes.ContinuarEmCasoDeErro && File.Exists(arquivoLog))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Atenção: Ocorreram erros em algumas linhas.");
            Console.WriteLine($"Consulte o arquivo: {arquivoLog}");
            Console.ResetColor();
        }
    }
}
