using System.Text;
using System.Text.Json;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl.Rules;

namespace SkyFBTool.Services.Ddl;

public static class AnalisadorDdlSchema
{
    public static async Task<(string ArquivoJson, string ArquivoHtml)> AnalisarAsync(OpcoesDdlAnalise opcoes)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();

        bool possuiEntradaArquivo = !string.IsNullOrWhiteSpace(opcoes.Entrada);
        bool possuiEntradaBanco = !string.IsNullOrWhiteSpace(opcoes.Database);
        bool possuiEntradaLote = !string.IsNullOrWhiteSpace(opcoes.DatabasesBatch);

        if (!possuiEntradaArquivo && !possuiEntradaBanco && !possuiEntradaLote)
        {
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Provide one input source: --input/--source, --database, or --databases-batch.",
                "Informe uma origem de entrada: --input/--source, --database ou --databases-batch."));
        }

        int totalOrigens = (possuiEntradaArquivo ? 1 : 0) + (possuiEntradaBanco ? 1 : 0) + (possuiEntradaLote ? 1 : 0);
        if (totalOrigens > 1)
        {
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Do not combine --input/--source, --database, and --databases-batch. Choose only one source.",
                "Não combine --input/--source, --database e --databases-batch. Escolha apenas uma origem."));
        }

        if (possuiEntradaBanco && ContemWildcard(opcoes.Database))
        {
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Wildcard in --database is not allowed. Use --databases-batch for batch mode.",
                "Wildcard em --database não é permitido. Use --databases-batch para modo em lote."));
        }

        if (possuiEntradaLote)
        {
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Batch mode must be handled by CLI command layer (--databases-batch).",
                "O modo em lote deve ser tratado pela camada de comando CLI (--databases-batch)."));
        }

        SnapshotSchema snapshot;
        string origemSnapshot;
        if (possuiEntradaBanco)
        {
            snapshot = await ExtratorDdlFirebird.ExtrairSnapshotAsync(new OpcoesDdlExtracao
            {
                Host = opcoes.Host,
                Porta = opcoes.Porta,
                Database = opcoes.Database,
                Usuario = opcoes.Usuario,
                Senha = opcoes.Senha,
                Charset = opcoes.Charset
            });

            origemSnapshot = opcoes.Database.Trim();
        }
        else
        {
            (snapshot, origemSnapshot) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(opcoes.Entrada);
        }

        Dictionary<string, string>? severidadesOverride = null;
        if (!string.IsNullOrWhiteSpace(opcoes.ArquivoConfiguracaoSeveridade))
            severidadesOverride = await ConfiguracaoSeveridadeDdl.CarregarAsync(opcoes.ArquivoConfiguracaoSeveridade, idioma);

        var resultado = Analisar(
            snapshot,
            idioma,
            origemSnapshot,
            opcoes.PrefixosTabelaIgnorados,
            severidadesOverride);

        resultado.Description = opcoes.Descricao?.Trim() ?? string.Empty;
        resultado.StatusAnaliseOperacional = possuiEntradaBanco ? "pending" : "not_applicable";
        resultado.AnaliseVolumeHabilitada = opcoes.AnaliseVolumeHabilitada;
        resultado.StatusAnaliseVolume = possuiEntradaBanco
            ? (opcoes.AnaliseVolumeHabilitada ? "pending" : "disabled")
            : "not_applicable";

        if (possuiEntradaBanco)
            await EnriquecerComAchadosOperacionaisAsync(resultado, opcoes, idioma, severidadesOverride);

        var (arquivoJsonSaida, arquivoHtmlSaida) = ResolverArquivosSaida(opcoes);
        Directory.CreateDirectory(Path.GetDirectoryName(arquivoJsonSaida)!);

        await File.WriteAllTextAsync(arquivoJsonSaida, JsonSerializer.Serialize(resultado, JsonOptions));
        await File.WriteAllTextAsync(arquivoHtmlSaida, RenderizadorHtmlAnaliseDdl.Renderizar(resultado, idioma), EncodingUtf8ComBom);

        return (arquivoJsonSaida, arquivoHtmlSaida);
    }

    public static ResultadoAnaliseDdl Analisar(
        SnapshotSchema snapshot,
        IdiomaSaida idioma = IdiomaSaida.English,
        string? origem = null,
        IEnumerable<string>? prefixosTabelaIgnorados = null,
        IReadOnlyDictionary<string, string>? severidadesOverride = null)
    {
        var resultado = new ResultadoAnaliseDdl
        {
            Origem = origem ?? string.Empty
        };

        var prefixosIgnorados = NormalizarPrefixos(prefixosTabelaIgnorados);
        var tabelasVisiveis = snapshot.Tabelas
            .Where(t => !DeveIgnorarTabela(t.Nome, prefixosIgnorados))
            .ToList();
        var tabelasIgnoradas = snapshot.Tabelas
            .Where(t => DeveIgnorarTabela(t.Nome, prefixosIgnorados))
            .Select(t => t.Nome)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        resultado.TotalTabelas = tabelasVisiveis.Count;

        var mapaTabelas = tabelasVisiveis.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);

        MotorRegrasAnaliseDdl.Executar(
            new ContextoAnaliseDdl(snapshot, resultado, idioma, tabelasVisiveis, mapaTabelas, tabelasIgnoradas, severidadesOverride),
            CriarRegrasAnalise());

        resultado.AtualizarResumo();

        return resultado;
    }

    private static IReadOnlyList<IRegraAnaliseDdl> CriarRegrasAnalise() =>
    [
        new RegraEstruturaTabelaDdl(),
        new RegraChavesEstrangeirasDdl(),
        new RegraIndicesDdl(),
        new RegraCompatibilidadeCamposDdl(),
        new RegraObjetosPsqlSemCorpoDdl(),
        new RegraObjetosPsqlSomenteSuspendDdl()
    ];

    private static List<string> NormalizarPrefixos(IEnumerable<string>? prefixos)
    {
        if (prefixos is null)
            return [];

        return prefixos
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool DeveIgnorarTabela(string nomeTabela, IReadOnlyCollection<string> prefixosIgnorados)
    {
        if (prefixosIgnorados.Count == 0)
            return false;

        foreach (var prefixo in prefixosIgnorados)
        {
            if (nomeTabela.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void AdicionarAchado(
        ResultadoAnaliseDdl resultado,
        string severidade,
        string codigo,
        string escopo,
        string descricao,
        string recomendacao,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        resultado.AdicionarAchado(severidade, codigo, escopo, descricao, recomendacao, severidadesOverride);
    }

    private static async Task EnriquecerComAchadosOperacionaisAsync(
        ResultadoAnaliseDdl resultado,
        OpcoesDdlAnalise opcoes,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        List<AchadoOperacionalDdl> achadosOperacionais = [];
        bool coletaOperacionalComSucesso = false;

        try
        {
            achadosOperacionais = await AnalisadorOperacionalFirebird.ColetarAchadosAsync(opcoes, idioma);
            coletaOperacionalComSucesso = true;
        }
        catch (Exception ex)
        {
            string classeFalha = AnalisadorOperacionalFirebird.ClassificarFalhaOperacional(ex);
            resultado.StatusAnaliseOperacional = classeFalha is "permission_denied" or "metadata_incompatible"
                ? "unavailable"
                : "failed";
            resultado.ErroAnaliseOperacional = AnalisadorOperacionalFirebird.FormatarErroColetaOperacional(ex);
            resultado.AchadosGeradosAnaliseOperacional = 0;
        }

        foreach (var achado in achadosOperacionais)
        {
            AdicionarAchado(
                resultado,
                achado.Severidade,
                achado.Codigo,
                achado.Escopo,
                achado.Descricao,
                achado.Recomendacao,
                severidadesOverride);
        }
        
        if (coletaOperacionalComSucesso)
        {
            resultado.StatusAnaliseOperacional = "executed";
            resultado.ErroAnaliseOperacional = string.Empty;
            resultado.AchadosGeradosAnaliseOperacional = achadosOperacionais.Count;
        }

        if (opcoes.AnaliseVolumeHabilitada)
        {
            try
            {
                var metricasVolume = await AnalisadorOperacionalFirebird.ColetarMetricasVolumeAsync(
                    opcoes,
                    usarCountExato: opcoes.AnaliseVolumeCountExato);
                int adicionados = AdicionarAchadosPrioridadeVolume(resultado, metricasVolume, idioma, severidadesOverride);
                resultado.StatusAnaliseVolume = "executed";
                resultado.TabelasLidasAnaliseVolume = metricasVolume.Count;
                resultado.AchadosGeradosAnaliseVolume = adicionados;
                resultado.ErroAnaliseVolume = string.Empty;
            }
            catch (Exception ex)
            {
                resultado.StatusAnaliseVolume = "failed";
                resultado.ErroAnaliseVolume = ex.Message;
            }
        }
        else
        {
            resultado.StatusAnaliseVolume = "disabled";
            resultado.ErroAnaliseVolume = string.Empty;
        }

        try
        {
            var dataUltimaManutencao = await AnalisadorOperacionalFirebird.ColetarDataUltimaManutencaoAsync(opcoes);
            if (dataUltimaManutencao is not null)
            {
                resultado.DataUltimaManutencaoUtc = dataUltimaManutencao;
                resultado.FonteDataUltimaManutencao = "MON$DATABASE.MON$CREATION_DATE";
            }
        }
        catch (Exception ex)
        {
            resultado.FonteDataUltimaManutencao = TextoLocalizado.Obter(
                idioma,
                $"MON$DATABASE.MON$CREATION_DATE (unavailable: {ex.GetType().Name}: {ex.Message})",
                $"MON$DATABASE.MON$CREATION_DATE (indisponível: {ex.GetType().Name}: {ex.Message})");
        }

        resultado.AtualizarResumo();
    }

    private static int AdicionarAchadosPrioridadeVolume(
        ResultadoAnaliseDdl resultado,
        IReadOnlyCollection<MetricaVolumeTabelaFirebird> metricasVolume,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        if (metricasVolume.Count == 0)
            return 0;

        int totalAdicionados = 0;

        var achadosPorTabela = resultado.Achados
            .Where(a => !a.Escopo.StartsWith("OPERACIONAL.", StringComparison.OrdinalIgnoreCase))
            .GroupBy(a => ResultadoAnaliseDdlExtensions.NomeTabelaDoEscopo(a.Escopo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var metrica in metricasVolume.OrderByDescending(m => m.RegistrosEstimados).Take(20))
        {
            if (!achadosPorTabela.TryGetValue(metrica.Tabela, out int totalAchadosTabela))
                continue;

            if (totalAchadosTabela <= 0)
                continue;

            string? severidade = null;
            string? codigo = null;

            if (metrica.RegistrosEstimados >= 10_000_000 && totalAchadosTabela >= 3)
            {
                severidade = "high";
                codigo = "OPERACIONAL_VOLUME_PRIORIDADE_ALTA";
            }
            else if (metrica.RegistrosEstimados >= 1_000_000 && totalAchadosTabela >= 2)
            {
                severidade = "medium";
                codigo = "OPERACIONAL_VOLUME_PRIORIDADE_MEDIA";
            }
            else if (metrica.RegistrosEstimados >= 500_000)
            {
                severidade = "low";
                codigo = "OPERACIONAL_VOLUME_PRIORIDADE_BAIXA";
            }

            if (severidade is null || codigo is null)
                continue;

            AdicionarAchado(
                resultado,
                severidade,
                codigo,
                metrica.Tabela,
                FormatarDescricaoPrioridadeVolume(
                    metrica.Tabela,
                    metrica.RegistrosEstimados,
                    totalAchadosTabela,
                    codigo,
                    idioma),
                FormatarRecomendacaoPrioridadeVolume(
                    metrica.Tabela,
                    codigo,
                    idioma),
                severidadesOverride);
            totalAdicionados++;
        }

        return totalAdicionados;
    }

    private static string FormatarDescricaoPrioridadeVolume(
        string tabela,
        long registrosEstimados,
        int totalAchadosTabela,
        string codigo,
        IdiomaSaida idioma)
    {
        return codigo switch
        {
            "OPERACIONAL_VOLUME_PRIORIDADE_ALTA" => TextoLocalizado.Obter(idioma,
                $"Table {tabela} has very high estimated volume ({registrosEstimados:N0} rows) and concentrated risk ({totalAchadosTabela} findings). Any regression here has high blast radius and can directly impact critical flows.",
                $"Tabela {tabela} tem volume estimado muito alto ({registrosEstimados:N0} registros) e risco concentrado ({totalAchadosTabela} achados). Qualquer regressão aqui tem alto impacto e pode afetar diretamente fluxos críticos."),
            "OPERACIONAL_VOLUME_PRIORIDADE_MEDIA" => TextoLocalizado.Obter(idioma,
                $"Table {tabela} has relevant estimated volume ({registrosEstimados:N0} rows) with recurring risk ({totalAchadosTabela} findings). Incidents here tend to cause cumulative performance degradation and operational instability.",
                $"Tabela {tabela} tem volume estimado relevante ({registrosEstimados:N0} registros) com risco recorrente ({totalAchadosTabela} achados). Problemas aqui tendem a gerar degradação cumulativa de performance e instabilidade operacional."),
            _ => TextoLocalizado.Obter(idioma,
                $"Table {tabela} has significant estimated volume ({registrosEstimados:N0} rows) with at least one structural finding ({totalAchadosTabela}). Isolated issues can become expensive over time due to high recurrence.",
                $"Tabela {tabela} tem volume estimado significativo ({registrosEstimados:N0} registros) com pelo menos um achado estrutural ({totalAchadosTabela}). Problemas isolados podem se tornar caros ao longo do tempo pela alta recorrência.")
        };
    }

    private static string FormatarRecomendacaoPrioridadeVolume(
        string tabela,
        string codigo,
        IdiomaSaida idioma)
    {
        return codigo switch
        {
            "OPERACIONAL_VOLUME_PRIORIDADE_ALTA" => TextoLocalizado.Obter(idioma,
                $"Treat {tabela} as immediate priority: review execution plans for the findings in this table, validate selective index coverage, and schedule remediation in the next release window.",
                $"Trate {tabela} como prioridade imediata: revise planos de execução dos achados dessa tabela, valide cobertura de índices seletivos e programe correção na próxima janela de release."),
            "OPERACIONAL_VOLUME_PRIORIDADE_MEDIA" => TextoLocalizado.Obter(idioma,
                $"Put {tabela} in the short-term remediation queue: validate hottest queries, confirm index usefulness, and execute corrections before growth amplifies current risk.",
                $"Coloque {tabela} na fila de correção de curto prazo: valide as consultas mais quentes, confirme utilidade dos índices e execute correções antes que o crescimento amplifique o risco atual."),
            _ => TextoLocalizado.Obter(idioma,
                $"Track {tabela} with planned remediation: confirm if the finding affects frequent queries and fix proactively to avoid latent cost escalation.",
                $"Acompanhe {tabela} com correção planejada: confirme se o achado afeta consultas frequentes e corrija de forma preventiva para evitar aumento de custo latente.")
        };
    }

    private static (string ArquivoJson, string ArquivoHtml) ResolverArquivosSaida(OpcoesDdlAnalise opcoes)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string padrao = $"schema_analysis_{timestamp}";

        if (string.IsNullOrWhiteSpace(opcoes.Saida))
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), padrao);
            return ($"{basePath}.json", $"{basePath}.html");
        }

        string saida = opcoes.Saida.Trim();
        if (Directory.Exists(saida) || saida.EndsWith(Path.DirectorySeparatorChar) || saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string basePath = Path.Combine(Path.GetFullPath(saida), padrao);
            return ($"{basePath}.json", $"{basePath}.html");
        }

        string semExtensao = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(saida)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(saida));

        return ($"{semExtensao}.json", $"{semExtensao}.html");
    }

    private static bool ContemWildcard(string valor)
    {
        return valor.Contains('*') || valor.Contains('?');
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly UTF8Encoding EncodingUtf8ComBom = new(encoderShouldEmitUTF8Identifier: true);

}
