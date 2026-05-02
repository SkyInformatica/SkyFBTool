using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Export;

public static class ExportadorTabelaFirebird
{
    private const long CheckpointUnidades = 50_000;
    private static readonly TimeSpan IntervaloCheckpoint = TimeSpan.FromSeconds(30);
    private const int MaxTentativasEscrita = 3;

    public static async Task ExportarAsync(OpcoesExportacao opcoes, IDestinoArquivo destino, IdiomaSaida idioma = IdiomaSaida.English)
    {
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(opcoes.Database))
            throw new ArgumentException(TextoLocalizado.Obter(idioma, "Database not provided (--database).", "Banco de dados não informado (--database)."));

        if (string.IsNullOrWhiteSpace(opcoes.Tabela))
            throw new ArgumentException(TextoLocalizado.Obter(idioma, "Table not provided (--table).", "Tabela não informada (--table)."));

        var tabelaOrigem = opcoes.Tabela;
        var tabelaDestino = string.IsNullOrWhiteSpace(opcoes.AliasTabela)
            ? tabelaOrigem
            : opcoes.AliasTabela;

        Console.WriteLine(TextoLocalizado.Obter(idioma, $"Starting export of table '{tabelaOrigem}' to '{tabelaDestino}'...", $"Iniciando exportação da tabela '{tabelaOrigem}' para '{tabelaDestino}'..."));
        
        await destino.EscreverLinhaAsync($"SET SQL DIALECT 3;");

        if (opcoes.Charset == null && opcoes.ForcarWin1252)
        {
            await destino.EscreverLinhaAsync($"SET NAMES WIN1252;");
        }
        else if (!string.IsNullOrWhiteSpace(opcoes.Charset))
        {
            await destino.EscreverLinhaAsync($"SET NAMES {opcoes.Charset};");
        }

        await destino.EscreverLinhaAsync(string.Empty);

        using var conexao = FabricaConexaoFirebird.CriarConexao(opcoes);
        await conexao.OpenAsync();

        var colunasPk = opcoes.ModoInsert == ModoInsertExportacao.Upsert
            ? await ObterColunasChavePrimariaAsync(conexao, tabelaOrigem)
            : Array.Empty<string>();

        if (opcoes.ModoInsert == ModoInsertExportacao.Upsert && colunasPk.Count == 0)
        {
            throw new InvalidOperationException(TextoLocalizado.Obter(idioma, $"Upsert mode requires a primary key. Table '{tabelaOrigem}' has no PK.", $"Modo upsert exige chave primária. A tabela '{tabelaOrigem}' não possui PK."));
        }

        if (!string.IsNullOrWhiteSpace(opcoes.ConsultaSqlCompleta))
        {
            var sqlSelect = ConstrutorConsultaFirebird.MontarSelect(opcoes, idioma);

            await using var cmd = new FbCommand(sqlSelect, conexao)
            {
                CommandTimeout = 0
            };

            await using var leitor = await cmd.ExecuteReaderAsync();
            var colunas = MontarColunasDaConsulta(leitor);
            var colunasMatching = ResolverColunasMatching(opcoes, tabelaOrigem, colunas, colunasPk, idioma);

            await ExportarLinhasAsync(opcoes, destino, leitor, tabelaDestino, colunas, colunasMatching, cronometro, idioma);
            return;
        }

        var colunasGravaveis = await ObterColunasGravaveisOrdenadasAsync(conexao, tabelaOrigem);
        if (colunasGravaveis.Count == 0)
            throw new InvalidOperationException(TextoLocalizado.Obter(idioma, "No writable columns were found for export.", "Nenhuma coluna gravável foi encontrada para exportação."));

        var sqlSelectComColunas = ConstrutorConsultaFirebird.MontarSelectComColunas(opcoes, colunasGravaveis, idioma);

        await using var cmdComColunas = new FbCommand(sqlSelectComColunas, conexao)
        {
            CommandTimeout = 0
        };

        await using var leitorComColunas = await cmdComColunas.ExecuteReaderAsync();
        var mapeamentoColunas = MontarColunasPorNome(leitorComColunas, colunasGravaveis);
        var colunasMatchingModoSimples = ResolverColunasMatching(opcoes, tabelaOrigem, mapeamentoColunas, colunasPk, idioma);
        await ExportarLinhasAsync(
            opcoes,
            destino,
            leitorComColunas,
            tabelaDestino,
            mapeamentoColunas,
            colunasMatchingModoSimples,
            cronometro,
            idioma);
    }

    private static async Task ExportarLinhasAsync(
        OpcoesExportacao opcoes,
        IDestinoArquivo destino,
        FbDataReader leitor,
        string tabelaDestino,
        (int Ordinal, string Nome)[] colunas,
        IReadOnlyList<string>? colunasMatching,
        System.Diagnostics.Stopwatch cronometro,
        IdiomaSaida idioma)
    {
        long totalLinhas = 0;
        long totalErros = 0;
        bool modoDinamico = !Console.IsOutputRedirected;
        var ultimoCheckpointEm = DateTime.UtcNow;
        long unidadesUltimoCheckpoint = 0;
        long linhasUltimaMedicao = 0;
        var cronometroVelocidade = System.Diagnostics.Stopwatch.StartNew();

        while (await leitor.ReadAsync())
        {
            totalLinhas++;
            linhasUltimaMedicao++;
            string insert;
            try
            {
                insert = ConstrutorInsert.MontarInsert(
                    leitor,
                    tabelaDestino,
                    colunas,
                    opcoes.ModoInsert,
                    colunasMatching,
                    opcoes.FormatoBlob,
                    opcoes.ForcarWin1252,
                    opcoes.SanitizarTexto,
                    opcoes.EscaparQuebrasDeLinha,
                    idioma);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    TextoLocalizado.Obter(idioma, $"Failed to generate INSERT for table '{tabelaDestino}' at row {totalLinhas}. {ex.Message}", $"Falha ao gerar INSERT da tabela '{tabelaDestino}' na linha {totalLinhas}. {ex.Message}"),
                    ex);
            }
            try
            {
                await EscreverLinhaComRetryAsync(destino, insert, idioma);
            }
            catch (Exception ex)
            {
                if (!opcoes.ContinuarEmCasoDeErro)
                    throw;

                totalErros++;
                File.AppendAllText("erros_exportacao.log",
                    TextoLocalizado.Obter(
                        idioma,
                        $"Error writing row {totalLinhas}: {ex.Message}{Environment.NewLine}",
                        $"Erro ao escrever linha {totalLinhas}: {ex.Message}{Environment.NewLine}"));
            }

            if (opcoes.CommitACada > 0 &&
                totalLinhas % opcoes.CommitACada == 0)
            {
                await EscreverLinhaComRetryAsync(destino, "COMMIT;", idioma);
            }

            AtualizarProgresso(
                totalLinhas,
                ref linhasUltimaMedicao,
                ref unidadesUltimoCheckpoint,
                ref ultimoCheckpointEm,
                cronometro,
                cronometroVelocidade,
                opcoes,
                modoDinamico,
                idioma);
        }

        if (totalLinhas > 0 && opcoes.CommitACada > 0)
        {
            await EscreverLinhaComRetryAsync(destino, "COMMIT;", idioma);
        }

        if (modoDinamico && opcoes.ProgressoACada > 0)
            Console.WriteLine();

        cronometro.Stop();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(TextoLocalizado.Obter(idioma, "Export completed successfully.", "Exportação concluída com sucesso."));
        Console.ResetColor();

        Console.WriteLine(TextoLocalizado.Obter(idioma, $"Exported rows: {totalLinhas:N0}", $"Linhas exportadas: {totalLinhas:N0}"));
        Console.WriteLine(TextoLocalizado.Obter(idioma, $"Errors:           {totalErros:N0}", $"Erros:             {totalErros:N0}"));
        Console.WriteLine(TextoLocalizado.Obter(idioma, $"Total time:       {cronometro.Elapsed:hh\\:mm\\:ss\\.fff}", $"Tempo total:       {cronometro.Elapsed:hh\\:mm\\:ss\\.fff}"));
    }

    private static void AtualizarProgresso(
        long linhas,
        ref long linhasUltimaMedicao,
        ref long unidadesUltimoCheckpoint,
        ref DateTime ultimoCheckpointEm,
        System.Diagnostics.Stopwatch cronometroTotal,
        System.Diagnostics.Stopwatch cronometroVelocidade,
        OpcoesExportacao opcoes,
        bool modoDinamico,
        IdiomaSaida idioma)
    {
        if (opcoes.ProgressoACada <= 0)
            return;

        if (linhas % opcoes.ProgressoACada == 0)
        {
            double segundos = cronometroVelocidade.Elapsed.TotalSeconds;
            double lps = segundos > 0 ? linhasUltimaMedicao / segundos : 0;

            string linha = TextoLocalizado.Obter(
                idioma,
                $"Processed: {linhas:N0} | Commands: {linhas:N0} | Speed: {lps:N0} cmd/s | Time: {FormatarDuracao(cronometroTotal.Elapsed)}",
                $"Processado: {linhas:N0} | Comandos: {linhas:N0} | Velocidade: {lps:N0} cmd/s | Tempo: {FormatarDuracao(cronometroTotal.Elapsed)}");
            if (modoDinamico)
                Console.Write($"\r{linha}");
            else
                Console.WriteLine(linha);

            linhasUltimaMedicao = 0;
            cronometroVelocidade.Restart();
        }

        bool checkpointPorUnidade = (linhas - unidadesUltimoCheckpoint) >= CheckpointUnidades;
        bool checkpointPorTempo = DateTime.UtcNow - ultimoCheckpointEm >= IntervaloCheckpoint;
        if (!checkpointPorUnidade && !checkpointPorTempo)
            return;

        double cpsCheckpoint = cronometroTotal.Elapsed.TotalSeconds > 0
            ? linhas / cronometroTotal.Elapsed.TotalSeconds
            : 0;

        if (modoDinamico)
            Console.WriteLine();

        Console.WriteLine(TextoLocalizado.Obter(
            idioma,
            $"Checkpoint: {linhas:N0} rows | {linhas:N0} commands | {FormatarDuracao(cronometroTotal.Elapsed)} | {cpsCheckpoint:N0} cmd/s",
            $"Checkpoint: {linhas:N0} linhas | {linhas:N0} comandos | {FormatarDuracao(cronometroTotal.Elapsed)} | {cpsCheckpoint:N0} cmd/s"));

        unidadesUltimoCheckpoint = linhas;
        ultimoCheckpointEm = DateTime.UtcNow;
    }

    private static string FormatarDuracao(TimeSpan tempo)
    {
        return $"{(int)tempo.TotalHours:00}:{tempo.Minutes:00}:{tempo.Seconds:00}.{tempo.Milliseconds:000}";
    }

    private static async Task EscreverLinhaComRetryAsync(IDestinoArquivo destino, string linha, IdiomaSaida idioma)
    {
        Exception? ultimoErro = null;

        for (int tentativa = 1; tentativa <= MaxTentativasEscrita; tentativa++)
        {
            try
            {
                await destino.EscreverLinhaAsync(linha);
                return;
            }
            catch (Exception ex) when (EhFalhaTransienteEscrita(ex) && tentativa < MaxTentativasEscrita)
            {
                ultimoErro = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(100 * tentativa));
            }
            catch (Exception ex)
            {
                ultimoErro = ex;
                break;
            }
        }

        throw ultimoErro ?? new InvalidOperationException(TextoLocalizado.Obter(idioma, "Failed to write line to destination file.", "Falha ao escrever linha no arquivo de destino."));
    }

    internal static bool EhFalhaTransienteEscrita(Exception ex)
    {
        if (ex is IOException || ex is TimeoutException)
            return true;

        string mensagem = ex.Message?.ToLowerInvariant() ?? string.Empty;
        return mensagem.Contains("being used by another process") ||
               mensagem.Contains("used by another process") ||
               mensagem.Contains("process cannot access the file") ||
               mensagem.Contains("temporarily unavailable") ||
               mensagem.Contains("resource temporarily unavailable");
    }

    private static async Task<IReadOnlyList<string>> ObterColunasGravaveisOrdenadasAsync(FbConnection conexao, string tabela)
    {
        const string sql = """
                           SELECT TRIM(rf.RDB$FIELD_NAME) AS CAMPO
                           FROM RDB$RELATION_FIELDS rf
                           JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE
                           WHERE UPPER(TRIM(rf.RDB$RELATION_NAME)) = @TABELA
                             AND f.RDB$COMPUTED_SOURCE IS NULL
                           ORDER BY rf.RDB$FIELD_POSITION
                           """;

        var colunas = new List<string>();
        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("TABELA", tabela.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            colunas.Add(reader.GetString(0).Trim());
        }

        return colunas;
    }

    private static (int Ordinal, string Nome)[] MontarColunasPorNome(FbDataReader leitor, IReadOnlyList<string> nomesColunas)
    {
        var colunas = new List<(int Ordinal, string Nome)>(nomesColunas.Count);
        foreach (var nomeColuna in nomesColunas)
        {
            int ordinal;
            try
            {
                ordinal = leitor.GetOrdinal(nomeColuna);
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new InvalidOperationException(
                    $"A coluna '{nomeColuna}' não foi retornada pela consulta de exportação.", ex);
            }

            colunas.Add((ordinal, nomeColuna));
        }

        return colunas.ToArray();
    }

    private static (int Ordinal, string Nome)[] MontarColunasDaConsulta(FbDataReader leitor)
    {
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var colunas = new List<(int Ordinal, string Nome)>(leitor.FieldCount);

        for (int i = 0; i < leitor.FieldCount; i++)
        {
            var nome = leitor.GetName(i)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nome))
                throw new InvalidOperationException($"A coluna na posição {i} da consulta não possui nome.");

            if (!nomes.Add(nome))
            {
                throw new InvalidOperationException(
                    $"A consulta possui colunas duplicadas com o nome '{nome}'. Use aliases únicos no --query-file.");
            }

            colunas.Add((i, nome));
        }

        if (colunas.Count == 0)
            throw new InvalidOperationException("A consulta não retornou colunas para exportação.");

        return colunas.ToArray();
    }

    private static IReadOnlyList<string>? ResolverColunasMatching(
        OpcoesExportacao opcoes,
        string tabelaOrigem,
        IReadOnlyList<(int Ordinal, string Nome)> colunasSelecionadas,
        IReadOnlyList<string> colunasPk,
        IdiomaSaida idioma)
    {
        if (opcoes.ModoInsert != ModoInsertExportacao.Upsert)
            return null;

        var colunasDisponiveis = new HashSet<string>(
            colunasSelecionadas.Select(c => c.Nome.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var colunasFaltantes = colunasPk
            .Where(pk => !colunasDisponiveis.Contains(pk))
            .ToArray();

        if (colunasFaltantes.Length > 0)
        {
            throw new InvalidOperationException(
                TextoLocalizado.Obter(idioma,
                    $"Upsert mode requires all PK columns in the query. Table '{tabelaOrigem}', missing: {string.Join(", ", colunasFaltantes)}.",
                    $"Modo upsert exige todas as colunas da PK na consulta. Tabela '{tabelaOrigem}', faltando: {string.Join(", ", colunasFaltantes)}."));
        }

        return colunasPk.ToArray();
    }

    private static async Task<IReadOnlyList<string>> ObterColunasChavePrimariaAsync(FbConnection conexao, string tabela)
    {
        const string sql = """
                           SELECT TRIM(seg.RDB$FIELD_NAME) AS CAMPO
                           FROM RDB$RELATION_CONSTRAINTS rc
                           JOIN RDB$INDEX_SEGMENTS seg ON seg.RDB$INDEX_NAME = rc.RDB$INDEX_NAME
                           WHERE UPPER(TRIM(rc.RDB$RELATION_NAME)) = @TABELA
                             AND UPPER(TRIM(rc.RDB$CONSTRAINT_TYPE)) = 'PRIMARY KEY'
                           ORDER BY seg.RDB$FIELD_POSITION
                           """;

        var colunas = new List<string>();
        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("TABELA", tabela.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            colunas.Add(reader.GetString(0).Trim());
        }

        return colunas;
    }
}
