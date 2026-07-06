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
        resultado.ObjetosAnalisados = CriarResumoObjetosAnalisados(snapshot, tabelasVisiveis);

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

    private static ResumoObjetosAnalisadosDdl CriarResumoObjetosAnalisados(
        SnapshotSchema snapshot,
        IReadOnlyCollection<TabelaSchema> tabelasVisiveis)
    {
        return new ResumoObjetosAnalisadosDdl
        {
            Tabelas = tabelasVisiveis.Count,
            Indices = tabelasVisiveis.Sum(t => t.Indices.Count),
            ChavesPrimarias = tabelasVisiveis.Count(t => t.ChavePrimaria is not null),
            ChavesEstrangeiras = tabelasVisiveis.Sum(t => t.ChavesEstrangeiras.Count),
            Triggers = snapshot.Gatilhos.Count,
            Procedures = snapshot.Procedimentos.Count,
            Functions = snapshot.Funcoes.Count
        };
    }

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
