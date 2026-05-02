using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Ddl;

public static class AnalisadorOperacionalFirebird
{
    public static string FormatarErroColetaOperacional(Exception ex)
    {
        string classe = ClassificarFalhaOperacional(ex);
        return $"{classe}: {ex.Message}";
    }

    public static string ClassificarFalhaOperacional(Exception ex)
    {
        string mensagem = (ex.Message ?? string.Empty).ToLowerInvariant();

        if (ex is TimeoutException ||
            mensagem.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "timeout";

        if (mensagem.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            mensagem.Contains("not permitted", StringComparison.OrdinalIgnoreCase) ||
            mensagem.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            mensagem.Contains("sql error code = -551", StringComparison.OrdinalIgnoreCase))
            return "permission_denied";

        if (mensagem.Contains("mon$", StringComparison.OrdinalIgnoreCase) &&
            (mensagem.Contains("column unknown", StringComparison.OrdinalIgnoreCase) ||
             mensagem.Contains("token unknown", StringComparison.OrdinalIgnoreCase) ||
             mensagem.Contains("sql error code = -206", StringComparison.OrdinalIgnoreCase)))
            return "metadata_incompatible";

        return "query_failure";
    }

    public static async Task<List<AchadoOperacionalDdl>> ColetarAchadosAsync(OpcoesDdlAnalise opcoes, IdiomaSaida idioma)
    {
        await using var conexao = FabricaConexaoFirebird.CriarConexao(
            opcoes.Host,
            opcoes.Porta,
            opcoes.Database,
            opcoes.Usuario,
            opcoes.Senha,
            opcoes.Charset);

        await conexao.OpenAsync();

        var metricas = await CarregarMetricasAsync(conexao);
        return Avaliar(metricas, idioma);
    }

    public static List<AchadoOperacionalDdl> Avaliar(MetricasOperacionaisFirebird metricas, IdiomaSaida idioma)
    {
        var achados = new List<AchadoOperacionalDdl>();

        int diferencaOitOat = metricas.OldestActive - metricas.OldestTransaction;
        if (diferencaOitOat >= 200_000)
        {
            achados.Add(CriarAchado(
                "critical",
                "OPERACIONAL_GAP_OIT_OAT_ELEVADO",
                "OPERACIONAL.TRANSACTIONS",
                M(
                    idioma,
                    $"High OAT-OIT gap detected ({diferencaOitOat:N0}).",
                    $"Gap OAT-OIT alto detectado ({diferencaOitOat:N0})."),
                M(
                    idioma,
                    "Investigate long-running transactions and retention pressure before maintenance windows.",
                    "Investigue transações longas e pressão de retenção antes de janelas de manutenção.")));
        }
        else if (diferencaOitOat >= 50_000)
        {
            achados.Add(CriarAchado(
                "high",
                "OPERACIONAL_GAP_OIT_OAT_ACIMA_DO_ESPERADO",
                "OPERACIONAL.TRANSACTIONS",
                M(
                    idioma,
                    $"OAT-OIT gap above expected threshold ({diferencaOitOat:N0}).",
                    $"Gap OAT-OIT acima do limite esperado ({diferencaOitOat:N0})."),
                M(
                    idioma,
                    "Review transaction lifecycle and housekeeping cadence (sweep/backup-restore strategy).",
                    "Revise o ciclo de vida de transações e a cadência de housekeeping (estratégia de sweep/backup-restore).")));
        }
        else if (diferencaOitOat >= 10_000)
        {
            achados.Add(CriarAchado(
                "medium",
                "OPERACIONAL_GAP_OIT_OAT_MODERADO",
                "OPERACIONAL.TRANSACTIONS",
                M(
                    idioma,
                    $"Moderate OAT-OIT gap detected ({diferencaOitOat:N0}).",
                    $"Gap OAT-OIT moderado detectado ({diferencaOitOat:N0})."),
                M(
                    idioma,
                    "Monitor transaction backlog growth and validate if cleanup cadence is sufficient.",
                    "Monitore crescimento do backlog transacional e valide se a cadência de limpeza está suficiente.")));
        }

        int diferencaOatOst = metricas.OldestSnapshot - metricas.OldestActive;
        if (diferencaOatOst >= 200_000)
        {
            achados.Add(CriarAchado(
                "high",
                "OPERACIONAL_GAP_OAT_OST_ELEVADO",
                "OPERACIONAL.SNAPSHOTS",
                M(
                    idioma,
                    $"High OST-OAT gap detected ({diferencaOatOst:N0}).",
                    $"Gap OST-OAT alto detectado ({diferencaOatOst:N0})."),
                M(
                    idioma,
                    "Inspect long snapshot transactions and reporting workloads that hold old versions.",
                    "Inspecione transações snapshot longas e cargas de relatório que mantêm versões antigas.")));
        }
        else if (diferencaOatOst >= 50_000)
        {
            achados.Add(CriarAchado(
                "medium",
                "OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO",
                "OPERACIONAL.SNAPSHOTS",
                M(
                    idioma,
                    $"OST-OAT gap above expected threshold ({diferencaOatOst:N0}).",
                    $"Gap OST-OAT acima do limite esperado ({diferencaOatOst:N0})."),
                M(
                    idioma,
                    "Review snapshot retention behavior and verify long-running read transactions.",
                    "Revise o comportamento de retenção snapshot e verifique transações de leitura longas.")));
        }

        if (metricas.MinTimestampTransacaoAtivaUtc is not null)
        {
            double idadeMinutos = (DateTime.UtcNow - metricas.MinTimestampTransacaoAtivaUtc.Value).TotalMinutes;

            if (idadeMinutos >= 720)
            {
                achados.Add(CriarAchado(
                    "critical",
                    "OPERACIONAL_TRANSACAO_ATIVA_LONGA_CRITICA",
                    "OPERACIONAL.TRANSACTIONS",
                    M(
                        idioma,
                        $"Critical long active transaction detected ({idadeMinutos:N0} minutes).",
                        $"Transação ativa longa crítica detectada ({idadeMinutos:N0} minutos)."),
                    M(
                        idioma,
                        "Prioritize transaction owner investigation and release retained versions.",
                        "Priorize investigação do dono da transação e liberação de versões retidas.")));
            }
            else if (idadeMinutos >= 120)
            {
                achados.Add(CriarAchado(
                    "high",
                    "OPERACIONAL_TRANSACAO_ATIVA_LONGA",
                    "OPERACIONAL.TRANSACTIONS",
                    M(
                        idioma,
                        $"Long active transaction detected ({idadeMinutos:N0} minutes).",
                        $"Transação ativa longa detectada ({idadeMinutos:N0} minutos)."),
                    M(
                        idioma,
                        "Review transaction scope in application and enforce shorter transactional boundaries.",
                        "Revise o escopo transacional na aplicação e force fronteiras transacionais mais curtas.")));
            }
            else if (idadeMinutos >= 30)
            {
                achados.Add(CriarAchado(
                    "medium",
                    "OPERACIONAL_TRANSACAO_ATIVA_ACIMA_DO_ESPERADO",
                    "OPERACIONAL.TRANSACTIONS",
                    M(
                        idioma,
                        $"Active transaction duration above expected threshold ({idadeMinutos:N0} minutes).",
                        $"Duração de transação ativa acima do esperado ({idadeMinutos:N0} minutos)."),
                    M(
                        idioma,
                        "Track transaction duration trend and tune long-running routines.",
                        "Acompanhe tendência de duração de transações e ajuste rotinas longas.")));
            }
        }

        return achados;
    }

    public static async Task<DateTime?> ColetarDataUltimaManutencaoAsync(OpcoesDdlAnalise opcoes)
    {
        await using var conexao = FabricaConexaoFirebird.CriarConexao(
            opcoes.Host,
            opcoes.Porta,
            opcoes.Database,
            opcoes.Usuario,
            opcoes.Senha,
            opcoes.Charset);

        await conexao.OpenAsync();
        var metricas = await CarregarMetricasAsync(conexao);
        return metricas.CreationDateUtc;
    }

    public static async Task<List<MetricaVolumeTabelaFirebird>> ColetarMetricasVolumeAsync(
        OpcoesDdlAnalise opcoes,
        bool usarCountExato = false,
        int timeoutSegundos = 10)
    {
        await using var conexao = FabricaConexaoFirebird.CriarConexao(
            opcoes.Host,
            opcoes.Porta,
            opcoes.Database,
            opcoes.Usuario,
            opcoes.Senha,
            opcoes.Charset);

        await conexao.OpenAsync();

        var tabelas = new List<string>();
        const string sqlTabelas = """
                                  SELECT TRIM(r.rdb$relation_name) AS relation_name
                                  FROM rdb$relations r
                                  WHERE r.rdb$view_blr IS NULL
                                    AND COALESCE(r.rdb$system_flag, 0) = 0
                                  ORDER BY 1
                                  """;

        await using (var cmdTabelas = new FbCommand(sqlTabelas, conexao))
        {
            cmdTabelas.CommandTimeout = Math.Max(1, timeoutSegundos);
            await using var readerTabelas = await cmdTabelas.ExecuteReaderAsync();
            while (await readerTabelas.ReadAsync())
                tabelas.Add(readerTabelas.GetString(0));
        }

        var resultado = new List<MetricaVolumeTabelaFirebird>();
        if (tabelas.Count == 0)
            return resultado;

        if (usarCountExato)
        {
            foreach (string tabela in tabelas)
            {
                string nomeTabelaSql = EscaparIdentificadorSql(tabela);
                string sqlCount = $"SELECT COUNT(*) FROM \"{nomeTabelaSql}\"";
                await using var cmdCount = new FbCommand(sqlCount, conexao)
                {
                    CommandTimeout = Math.Max(1, timeoutSegundos)
                };

                object? valor = await cmdCount.ExecuteScalarAsync();
                long registros = valor is null || valor == DBNull.Value ? 0L : Convert.ToInt64(valor);

                resultado.Add(new MetricaVolumeTabelaFirebird
                {
                    Tabela = tabela,
                    RegistrosEstimados = registros
                });
            }

            return resultado;
        }

        const string sqlVolumeEstimadoPorIndice = """
                                                  SELECT
                                                      t.relation_name,
                                                      COALESCE(
                                                          CAST(ROUND(1 / NULLIF(MIN(
                                                              CASE
                                                                  WHEN i.rdb$unique_flag = 1 OR rc.rdb$constraint_type = 'PRIMARY KEY' THEN i.rdb$statistics
                                                                  ELSE NULL
                                                              END
                                                          ), 0), 0) AS BIGINT),
                                                          CAST(ROUND(1 / NULLIF(MIN(i.rdb$statistics), 0), 0) AS BIGINT),
                                                          0
                                                      ) AS estimated_rows
                                                  FROM (
                                                      SELECT TRIM(r.rdb$relation_name) AS relation_name
                                                      FROM rdb$relations r
                                                      WHERE r.rdb$view_blr IS NULL
                                                        AND COALESCE(r.rdb$system_flag, 0) = 0
                                                  ) t
                                                  LEFT JOIN rdb$indices i
                                                    ON TRIM(i.rdb$relation_name) = t.relation_name
                                                   AND COALESCE(i.rdb$system_flag, 0) = 0
                                                   AND i.rdb$expression_source IS NULL
                                                   AND COALESCE(i.rdb$index_inactive, 0) = 0
                                                  LEFT JOIN rdb$relation_constraints rc
                                                    ON TRIM(rc.rdb$index_name) = TRIM(i.rdb$index_name)
                                                  GROUP BY t.relation_name
                                                  ORDER BY 2 DESC, 1
                                                  """;

        await using var cmd = new FbCommand(sqlVolumeEstimadoPorIndice, conexao)
        {
            CommandTimeout = Math.Max(1, timeoutSegundos)
        };
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string tabela = reader.GetString(0);
            long estimado = ConverterParaLong(reader.GetValue(1));
            resultado.Add(new MetricaVolumeTabelaFirebird
            {
                Tabela = tabela,
                RegistrosEstimados = estimado
            });
        }

        return resultado;
    }

    private static async Task<MetricasOperacionaisFirebird> CarregarMetricasAsync(FbConnection conexao)
    {
        const string sqlDatabase = """
                                   SELECT
                                       COALESCE(mon$oldest_transaction, 0) AS oit,
                                       COALESCE(mon$oldest_active, 0) AS oat,
                                       COALESCE(mon$oldest_snapshot, 0) AS ost,
                                       COALESCE(mon$next_transaction, 0) AS nxt,
                                       mon$creation_date AS creation_date
                                   FROM mon$database
                                   """;

        const string sqlMinAtiva = """
                                    SELECT MIN(mon$timestamp)
                                    FROM mon$transactions
                                    WHERE mon$state = 1
                                      AND mon$attachment_id <> CURRENT_CONNECTION
                                    """;

        var metricas = new MetricasOperacionaisFirebird();

        await using (var cmd = new FbCommand(sqlDatabase, conexao))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                metricas.OldestTransaction = ConverterParaInt(reader.GetValue(0));
                metricas.OldestActive = ConverterParaInt(reader.GetValue(1));
                metricas.OldestSnapshot = ConverterParaInt(reader.GetValue(2));
                metricas.NextTransaction = ConverterParaInt(reader.GetValue(3));
                if (!reader.IsDBNull(4))
                    metricas.CreationDateUtc = ConverterParaUtc(reader.GetValue(4));
            }
        }

        await using (var cmd = new FbCommand(sqlMinAtiva, conexao))
        {
            object? valor = await cmd.ExecuteScalarAsync();
            if (valor is not null && valor != DBNull.Value)
                metricas.MinTimestampTransacaoAtivaUtc = ConverterParaUtc(valor);
        }

        return metricas;
    }

    private static AchadoOperacionalDdl CriarAchado(
        string severidade,
        string codigo,
        string escopo,
        string descricao,
        string recomendacao)
    {
        return new AchadoOperacionalDdl
        {
            Severidade = severidade,
            Codigo = codigo,
            Escopo = escopo,
            Descricao = descricao,
            Recomendacao = recomendacao
        };
    }

    private static int ConverterParaInt(object valor)
    {
        return Convert.ToInt32(valor);
    }

    private static long ConverterParaLong(object valor)
    {
        return Convert.ToInt64(Math.Round(Convert.ToDouble(valor), MidpointRounding.AwayFromZero));
    }

    private static string EscaparIdentificadorSql(string nome)
    {
        return nome.Replace("\"", "\"\"");
    }

    private static DateTime ConverterParaUtc(object valor)
    {
        if (valor is DateTimeOffset dto)
            return dto.UtcDateTime;

        if (valor is DateTime dt)
            return dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();

        return Convert.ToDateTime(valor).ToUniversalTime();
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}

public sealed class MetricasOperacionaisFirebird
{
    public int OldestTransaction { get; set; }
    public int OldestActive { get; set; }
    public int OldestSnapshot { get; set; }
    public int NextTransaction { get; set; }
    public DateTime? MinTimestampTransacaoAtivaUtc { get; set; }
    public DateTime? CreationDateUtc { get; set; }
}

public sealed class AchadoOperacionalDdl
{
    public string Severidade { get; set; } = "low";
    public string Codigo { get; set; } = string.Empty;
    public string Escopo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Recomendacao { get; set; } = string.Empty;
}

public sealed class MetricaVolumeTabelaFirebird
{
    public string Tabela { get; set; } = string.Empty;
    public long RegistrosEstimados { get; set; }
}
