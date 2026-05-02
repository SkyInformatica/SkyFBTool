using System.Text.Json;
using SkyFBTool.Core;

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
            throw new ArgumentException(M(
                idioma,
                "Provide one input source: --input/--source, --database, or --databases-batch.",
                "Informe uma origem de entrada: --input/--source, --database ou --databases-batch."));
        }

        int totalOrigens = (possuiEntradaArquivo ? 1 : 0) + (possuiEntradaBanco ? 1 : 0) + (possuiEntradaLote ? 1 : 0);
        if (totalOrigens > 1)
        {
            throw new ArgumentException(M(
                idioma,
                "Do not combine --input/--source, --database, and --databases-batch. Choose only one source.",
                "Não combine --input/--source, --database e --databases-batch. Escolha apenas uma origem."));
        }

        if (possuiEntradaBanco && ContemWildcard(opcoes.Database))
        {
            throw new ArgumentException(M(
                idioma,
                "Wildcard in --database is not allowed. Use --databases-batch for batch mode.",
                "Wildcard em --database não é permitido. Use --databases-batch para modo em lote."));
        }

        if (possuiEntradaLote)
        {
            throw new ArgumentException(M(
                idioma,
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
            severidadesOverride = await ConfiguracaoSeveridadeDdl.CarregarAsync(opcoes.ArquivoConfiguracaoSeveridade);

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
        await File.WriteAllTextAsync(arquivoHtmlSaida, RenderizadorHtmlAnaliseDdl.Renderizar(resultado, idioma));

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

        foreach (var tabela in tabelasVisiveis.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarTabelaSemColunas(tabela, resultado, idioma, severidadesOverride);
            ValidarColunasDuplicadas(tabela, resultado, idioma, severidadesOverride);
            ValidarTiposDesconhecidos(tabela, resultado, idioma, severidadesOverride);
            ValidarPk(tabela, resultado, idioma, severidadesOverride);
            ValidarFks(tabela, mapaTabelas, tabelasIgnoradas, resultado, idioma, severidadesOverride);
            ValidarIndices(tabela, resultado, idioma, severidadesOverride);
            ValidarDuplicidadeIndices(tabela, resultado, idioma, severidadesOverride);
            ValidarRedundanciaIndicesPrefixo(tabela, resultado, idioma, severidadesOverride);
            ValidarDuplicidadeFks(tabela, resultado, idioma, severidadesOverride);
        }

        resultado.Achados = resultado.Achados
            .OrderByDescending(a => a.ScoreRisco)
            .ThenByDescending(a => PesoSeveridade(a.Severidade))
            .ThenBy(a => a.Codigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Escopo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        resultado.TotalAchados = resultado.Achados.Count;
        resultado.TotalCriticos = resultado.Achados.Count(a => a.Severidade == "critical");
        resultado.TotalAltos = resultado.Achados.Count(a => a.Severidade == "high");
        resultado.TotalMedios = resultado.Achados.Count(a => a.Severidade == "medium");
        resultado.TotalBaixos = resultado.Achados.Count(a => a.Severidade == "low");
        resultado.ResumoPorCodigo = MontarResumo(resultado.Achados, a => a.Codigo);
        resultado.ResumoPorTabela = MontarResumo(resultado.Achados, a => NomeTabelaDoEscopo(a.Escopo));

        return resultado;
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

    private static void ValidarTabelaSemColunas(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        if (tabela.Colunas.Count > 0)
            return;

        AdicionarAchado(
            resultado,
            "critical",
            "TABELA_SEM_COLUNAS",
            tabela.Nome,
            M(idioma, $"Table {tabela.Nome} has no columns.", $"Tabela {tabela.Nome} não possui colunas."),
            M(idioma, "Re-extract metadata and validate this table directly in system catalogs.", "Reextraia o metadado e valide esta tabela diretamente nos catálogos do Firebird."),
            severidadesOverride);
    }

    private static void ValidarColunasDuplicadas(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        var duplicadas = tabela.Colunas
            .GroupBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var coluna in duplicadas.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            AdicionarAchado(
                resultado,
                "critical",
                "COLUNA_DUPLICADA",
                $"{tabela.Nome}.{coluna}",
                M(idioma, $"Duplicated column in table {tabela.Nome}: {coluna}.", $"Coluna duplicada na tabela {tabela.Nome}: {coluna}."),
                M(idioma, "Inspect metadata consistency and rebuild affected objects if needed.", "Inspecione consistência de metadados e recrie objetos afetados se necessário."),
                severidadesOverride);
        }
    }

    private static void ValidarTiposDesconhecidos(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        foreach (var coluna in tabela.Colunas)
        {
            if (!coluna.TipoSql.StartsWith("TYPE_", StringComparison.OrdinalIgnoreCase))
                continue;

            AdicionarAchado(
                resultado,
                "high",
                "TIPO_DESCONHECIDO",
                $"{tabela.Nome}.{coluna.Nome}",
                M(
                    idioma,
                    $"Column {tabela.Nome}.{coluna.Nome} has unknown type mapping: {coluna.TipoSql}.",
                    $"Coluna {tabela.Nome}.{coluna.Nome} possui mapeamento de tipo desconhecido: {coluna.TipoSql}."),
                M(idioma, "Validate database version compatibility and inspect field definition in RDB$FIELDS.", "Valide compatibilidade de versão e confira a definição no RDB$FIELDS."),
                severidadesOverride);
        }
    }

    private static void ValidarPk(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        if (tabela.ChavePrimaria is null)
        {
            AdicionarAchado(
                resultado,
                "high",
                "TABELA_SEM_PK",
                tabela.Nome,
                M(idioma, $"Table {tabela.Nome} has no primary key.", $"Tabela {tabela.Nome} não possui chave primária."),
                M(idioma, "Review if this is expected. Missing PK may hide duplicate rows over time.", "Revise se isso é esperado. Ausência de PK pode mascarar duplicidades."),
                severidadesOverride);
            return;
        }

        if (tabela.ChavePrimaria.Colunas.Count == 0)
        {
            AdicionarAchado(
                resultado,
                "critical",
                "PK_SEM_COLUNAS",
                tabela.Nome,
                M(idioma, $"Primary key {tabela.ChavePrimaria.Nome} in {tabela.Nome} has no columns.", $"Chave primária {tabela.ChavePrimaria.Nome} em {tabela.Nome} não possui colunas."),
                M(idioma, "Rebuild this PK from validated column metadata.", "Recrie esta PK a partir de metadados validados."),
                severidadesOverride);
            return;
        }

        var colunas = tabela.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        foreach (var colunaPk in tabela.ChavePrimaria.Colunas)
        {
            if (colunas.ContainsKey(colunaPk))
                continue;

            AdicionarAchado(
                resultado,
                "critical",
                "PK_REFERENCIA_COLUNA_INEXISTENTE",
                tabela.Nome,
                M(idioma, $"PK {tabela.ChavePrimaria.Nome} references missing column {colunaPk}.", $"PK {tabela.ChavePrimaria.Nome} referencia coluna inexistente {colunaPk}."),
                M(idioma, "Recreate PK and validate relation fields catalog.", "Recrie a PK e valide o catálogo de campos da relação."),
                severidadesOverride);
        }
    }

    private static void ValidarFks(
        TabelaSchema tabela,
        IReadOnlyDictionary<string, TabelaSchema> tabelas,
        IReadOnlySet<string> tabelasIgnoradas,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        var colunasLocais = tabela.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var fk in tabela.ChavesEstrangeiras)
        {
            string escopo = $"{tabela.Nome}.{fk.Nome}";

            if (!ValidarEstruturaFk(fk, escopo, resultado, idioma, severidadesOverride))
                continue;

            ValidarColunasLocaisFk(fk, colunasLocais, escopo, resultado, idioma, severidadesOverride);

            if (tabelasIgnoradas.Contains(fk.TabelaReferencia))
                continue;

            ValidarReferenciaFk(fk, tabelas, escopo, resultado, idioma, severidadesOverride);
            ValidarIndiceCoberturaFk(tabela, fk, escopo, resultado, idioma, severidadesOverride);
        }
    }

    private static bool ValidarEstruturaFk(
        ChaveEstrangeiraSchema fk,
        string escopo,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        if (fk.Colunas.Count == 0 || fk.ColunasReferencia.Count == 0)
        {
            AdicionarAchado(
                resultado,
                "critical",
                "FK_SEM_COLUNAS",
                escopo,
                M(idioma, $"FK {fk.Nome} has empty local/reference columns.", $"FK {fk.Nome} possui colunas locais/referência vazias."),
                M(idioma, "Recreate FK with explicit and ordered column list.", "Recrie a FK com lista de colunas explícita e ordenada."),
                severidadesOverride);
            return false;
        }

        if (fk.Colunas.Count != fk.ColunasReferencia.Count)
        {
            AdicionarAchado(
                resultado,
                "critical",
                "FK_CARDINALIDADE_INVALIDA",
                escopo,
                M(idioma, $"FK {fk.Nome} has different local/reference column counts.", $"FK {fk.Nome} possui cardinalidade diferente entre colunas locais e de referência."),
                M(idioma, "Recreate FK preserving matching column cardinality.", "Recrie a FK preservando cardinalidade equivalente."),
                severidadesOverride);
        }

        return true;
    }

    private static void ValidarColunasLocaisFk(
        ChaveEstrangeiraSchema fk,
        IReadOnlyDictionary<string, ColunaSchema> colunasLocais,
        string escopo,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        foreach (var colunaFk in fk.Colunas)
        {
            if (colunasLocais.ContainsKey(colunaFk))
                continue;

            AdicionarAchado(
                resultado,
                "critical",
                "FK_COLUNA_LOCAL_INEXISTENTE",
                escopo,
                M(idioma, $"FK {fk.Nome} references missing local column {colunaFk}.", $"FK {fk.Nome} referencia coluna local inexistente {colunaFk}."),
                M(idioma, "Validate relation fields and rebuild FK.", "Valide os campos da relação e recrie a FK."),
                severidadesOverride);
        }
    }

    private static void ValidarReferenciaFk(
        ChaveEstrangeiraSchema fk,
        IReadOnlyDictionary<string, TabelaSchema> tabelas,
        string escopo,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        if (!tabelas.TryGetValue(fk.TabelaReferencia, out var tabelaReferencia))
        {
            AdicionarAchado(
                resultado,
                "critical",
                "FK_TABELA_REFERENCIA_INEXISTENTE",
                escopo,
                M(idioma, $"FK {fk.Nome} points to missing table {fk.TabelaReferencia}.", $"FK {fk.Nome} aponta para tabela inexistente {fk.TabelaReferencia}."),
                M(idioma, "Validate dependency order and metadata integrity for referenced table.", "Valide ordem de dependência e integridade de metadados da tabela referenciada."),
                severidadesOverride);
            return;
        }

        var colunasReferencia = tabelaReferencia.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        foreach (var colunaRef in fk.ColunasReferencia)
        {
            if (colunasReferencia.ContainsKey(colunaRef))
                continue;

            AdicionarAchado(
                resultado,
                "critical",
                "FK_COLUNA_REFERENCIA_INEXISTENTE",
                escopo,
                M(idioma, $"FK {fk.Nome} points to missing referenced column {colunaRef}.", $"FK {fk.Nome} aponta para coluna referenciada inexistente {colunaRef}."),
                M(idioma, "Recreate FK after validating referenced key definition.", "Recrie a FK após validar a definição da chave referenciada."),
                severidadesOverride);
        }
    }

    private static void ValidarIndiceCoberturaFk(
        TabelaSchema tabela,
        ChaveEstrangeiraSchema fk,
        string escopo,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        if (!string.IsNullOrWhiteSpace(fk.IndiceSuporteNome))
            return;

        bool possuiIndiceCobertura = tabela.Indices.Any(indice => CobrePrefixo(indice.Colunas, fk.Colunas));
        if (possuiIndiceCobertura)
            return;

        AdicionarAchado(
            resultado,
            "medium",
            "FK_SEM_INDICE_COBERTURA",
            escopo,
            M(
                idioma,
                $"FK {fk.Nome} has no local covering index. Child table: {tabela.Nome} ({FormatarListaColunas(fk.Colunas)}). Parent table: {fk.TabelaReferencia} ({FormatarListaColunas(fk.ColunasReferencia)}).",
                $"FK {fk.Nome} não possui índice local de cobertura. Tabela filha: {tabela.Nome} ({FormatarListaColunas(fk.Colunas)}). Tabela pai: {fk.TabelaReferencia} ({FormatarListaColunas(fk.ColunasReferencia)})."),
            M(
                idioma,
                $"Create an index on child table {tabela.Nome} using FK columns ({FormatarListaColunas(fk.Colunas)}), preserving FK column order.",
                $"Crie um índice na tabela filha {tabela.Nome} usando as colunas da FK ({FormatarListaColunas(fk.Colunas)}), preservando a ordem da FK."),
            severidadesOverride);
    }

    private static void ValidarIndices(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        var colunas = tabela.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var indice in tabela.Indices)
        {
            string escopo = $"{tabela.Nome}.{indice.Nome}";

            if (indice.Colunas.Count == 0)
            {
                AdicionarAchado(
                    resultado,
                    "high",
                    "INDICE_SEM_COLUNAS",
                    escopo,
                    M(idioma, $"Index {indice.Nome} has no columns.", $"Índice {indice.Nome} não possui colunas."),
                    M(idioma, "Recreate index with explicit column list.", "Recrie o índice com lista explícita de colunas."),
                    severidadesOverride);
                continue;
            }

            foreach (var colunaIndice in indice.Colunas)
            {
                if (colunas.ContainsKey(colunaIndice))
                    continue;

                AdicionarAchado(
                    resultado,
                    "high",
                    "INDICE_COLUNA_INEXISTENTE",
                    escopo,
                    M(idioma, $"Index {indice.Nome} references missing column {colunaIndice}.", $"Índice {indice.Nome} referencia coluna inexistente {colunaIndice}."),
                    M(idioma, "Recreate index and validate relation fields catalog.", "Recrie o índice e valide o catálogo de campos da relação."),
                    severidadesOverride);
            }
        }
    }

    private static void ValidarDuplicidadeIndices(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        var grupos = tabela.Indices
            .GroupBy(AssinaturaIndice, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var grupo in grupos)
        {
            string nomes = string.Join(", ", grupo.Select(i => i.Nome).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            string assinaturaExibicao = FormatarAssinaturaComparacaoIndice(grupo.First());
            AdicionarAchado(
                resultado,
                "low",
                "INDICE_DUPLICADO",
                tabela.Nome,
                M(
                    idioma,
                    $"Duplicated index signature in {tabela.Nome}: {nomes}. Signature: {assinaturaExibicao}.",
                    $"Assinatura de índice duplicada em {tabela.Nome}: {nomes}. Assinatura: {assinaturaExibicao}."),
                M(idioma, "Keep only one index per signature after workload validation.", "Mantenha apenas um índice por assinatura após validar carga de trabalho."),
                severidadesOverride);
        }
    }

    private static void ValidarRedundanciaIndicesPrefixo(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        var indices = tabela.Indices
            .Where(i => i.Colunas.Count > 0)
            .OrderBy(i => i.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < indices.Count; i++)
        {
            var indiceCurto = indices[i];

            for (int j = 0; j < indices.Count; j++)
            {
                if (i == j)
                    continue;

                var indiceLongo = indices[j];
                if (indiceCurto.Unico || indiceLongo.Unico)
                    continue;

                if (indiceCurto.Descendente != indiceLongo.Descendente)
                    continue;

                if (!EhPrefixo(indiceCurto.Colunas, indiceLongo.Colunas))
                    continue;

                if (indiceCurto.Colunas.Count == indiceLongo.Colunas.Count)
                    continue;

                AdicionarAchado(
                    resultado,
                    "medium",
                    "INDICE_REDUNDANTE_PREFIXO",
                    tabela.Nome,
                    M(
                        idioma,
                        $"Index {indiceCurto.Nome} ({FormatarAssinaturaComparacaoIndice(indiceCurto)}) may be redundant because {indiceLongo.Nome} ({FormatarAssinaturaComparacaoIndice(indiceLongo)}) already covers its prefix ({FormatarListaColunas(indiceCurto.Colunas)}).",
                        $"Índice {indiceCurto.Nome} ({FormatarAssinaturaComparacaoIndice(indiceCurto)}) pode ser redundante porque {indiceLongo.Nome} ({FormatarAssinaturaComparacaoIndice(indiceLongo)}) já cobre seu prefixo ({FormatarListaColunas(indiceCurto.Colunas)})."),
                    M(
                        idioma,
                        "Validate query plans and keep only the index with better selectivity/coverage.",
                        "Valide planos de execução e mantenha apenas o índice com melhor seletividade/cobertura."),
                    severidadesOverride);

                break;
            }
        }
    }

    private static void ValidarDuplicidadeFks(
        TabelaSchema tabela,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        var grupos = tabela.ChavesEstrangeiras
            .GroupBy(AssinaturaFk, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var grupo in grupos)
        {
            string nomes = string.Join(", ", grupo.Select(f => f.Nome).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            AdicionarAchado(
                resultado,
                "low",
                "FK_DUPLICADA",
                tabela.Nome,
                M(idioma, $"Duplicated FK signature in {tabela.Nome}: {nomes}.", $"Assinatura de FK duplicada em {tabela.Nome}: {nomes}."),
                M(idioma, "Consolidate equivalent foreign keys and keep only one validated constraint.", "Consolide FKs equivalentes e mantenha apenas uma restrição validada."),
                severidadesOverride);
        }
    }

    private static List<ItemResumoAnaliseDdl> MontarResumo(IReadOnlyCollection<AchadoAnaliseDdl> achados, Func<AchadoAnaliseDdl, string> chave)
    {
        if (achados.Count == 0)
            return [];

        return achados
            .GroupBy(chave, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ItemResumoAnaliseDdl
            {
                Chave = g.Key,
                Quantidade = g.Count(),
                Percentual = Math.Round((decimal)g.Count() * 100m / achados.Count, 2)
            })
            .OrderByDescending(i => i.Quantidade)
            .ThenBy(i => i.Chave, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NomeTabelaDoEscopo(string escopo)
    {
        if (string.IsNullOrWhiteSpace(escopo))
            return "?";

        int idx = escopo.IndexOf('.');
        return idx < 0 ? escopo : escopo[..idx];
    }

    private static string AssinaturaIndice(IndiceSchema indice)
    {
        return $"{(indice.Unico ? "U" : "N")}|{(indice.Descendente ? "D" : "A")}|{string.Join("|", indice.Colunas).ToUpperInvariant()}";
    }

    private static string AssinaturaFk(ChaveEstrangeiraSchema fk)
    {
        return string.Join("|", fk.Colunas).ToUpperInvariant()
               + "->" + fk.TabelaReferencia.ToUpperInvariant()
               + "(" + string.Join("|", fk.ColunasReferencia).ToUpperInvariant() + ")"
               + $"[{fk.RegraUpdate.ToUpperInvariant()}|{fk.RegraDelete.ToUpperInvariant()}]";
    }

    private static bool CobrePrefixo(IReadOnlyList<string> colunasIndice, IReadOnlyList<string> colunasFk)
    {
        if (colunasFk.Count == 0 || colunasIndice.Count < colunasFk.Count)
            return false;

        for (int i = 0; i < colunasFk.Count; i++)
        {
            if (!string.Equals(colunasIndice[i], colunasFk[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool EhPrefixo(IReadOnlyList<string> colunasCurtas, IReadOnlyList<string> colunasLongas)
    {
        if (colunasCurtas.Count == 0 || colunasLongas.Count < colunasCurtas.Count)
            return false;

        for (int i = 0; i < colunasCurtas.Count; i++)
        {
            if (!string.Equals(colunasCurtas[i], colunasLongas[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string FormatarListaColunas(IReadOnlyList<string> colunas)
    {
        if (colunas.Count == 0)
            return "-";

        return string.Join(", ", colunas);
    }

    private static string FormatarAssinaturaComparacaoIndice(IndiceSchema indice)
    {
        string ordem = indice.Descendente ? "DESC" : "ASC";
        string unicidade = indice.Unico ? "UNIQUE" : "NON-UNIQUE";
        return $"{unicidade}, {ordem}, ({FormatarListaColunas(indice.Colunas)})";
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
        string severidadeFinal = ConfiguracaoSeveridadeDdl.AplicarOverride(codigo, severidade, severidadesOverride);
        int scoreRisco = CalcularScoreRisco(severidadeFinal, codigo);

        resultado.Achados.Add(new AchadoAnaliseDdl
        {
            Severidade = severidadeFinal,
            ScoreRisco = scoreRisco,
            Prioridade = CalcularPrioridade(scoreRisco),
            Codigo = codigo,
            Escopo = escopo,
            Descricao = descricao,
            Recomendacao = recomendacao
        });
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
            resultado.StatusAnaliseOperacional = "failed";
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
        catch
        {
        }

        resultado.Achados = resultado.Achados
            .OrderByDescending(a => a.ScoreRisco)
            .ThenByDescending(a => PesoSeveridade(a.Severidade))
            .ThenBy(a => a.Codigo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Escopo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        resultado.TotalAchados = resultado.Achados.Count;
        resultado.TotalCriticos = resultado.Achados.Count(a => a.Severidade == "critical");
        resultado.TotalAltos = resultado.Achados.Count(a => a.Severidade == "high");
        resultado.TotalMedios = resultado.Achados.Count(a => a.Severidade == "medium");
        resultado.TotalBaixos = resultado.Achados.Count(a => a.Severidade == "low");
        resultado.ResumoPorCodigo = MontarResumo(resultado.Achados, a => a.Codigo);
        resultado.ResumoPorTabela = MontarResumo(resultado.Achados, a => NomeTabelaDoEscopo(a.Escopo));
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
            .GroupBy(a => NomeTabelaDoEscopo(a.Escopo), StringComparer.OrdinalIgnoreCase)
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
            "OPERACIONAL_VOLUME_PRIORIDADE_ALTA" => M(
                idioma,
                $"Table {tabela} has very high estimated volume ({registrosEstimados:N0} rows) and concentrated risk ({totalAchadosTabela} findings). Any regression here has high blast radius and can directly impact critical flows.",
                $"Tabela {tabela} tem volume estimado muito alto ({registrosEstimados:N0} registros) e risco concentrado ({totalAchadosTabela} achados). Qualquer regressão aqui tem alto impacto e pode afetar diretamente fluxos críticos."),
            "OPERACIONAL_VOLUME_PRIORIDADE_MEDIA" => M(
                idioma,
                $"Table {tabela} has relevant estimated volume ({registrosEstimados:N0} rows) with recurring risk ({totalAchadosTabela} findings). Incidents here tend to cause cumulative performance degradation and operational instability.",
                $"Tabela {tabela} tem volume estimado relevante ({registrosEstimados:N0} registros) com risco recorrente ({totalAchadosTabela} achados). Problemas aqui tendem a gerar degradação cumulativa de performance e instabilidade operacional."),
            _ => M(
                idioma,
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
            "OPERACIONAL_VOLUME_PRIORIDADE_ALTA" => M(
                idioma,
                $"Treat {tabela} as immediate priority: review execution plans for the findings in this table, validate selective index coverage, and schedule remediation in the next release window.",
                $"Trate {tabela} como prioridade imediata: revise planos de execução dos achados dessa tabela, valide cobertura de índices seletivos e programe correção na próxima janela de release."),
            "OPERACIONAL_VOLUME_PRIORIDADE_MEDIA" => M(
                idioma,
                $"Put {tabela} in the short-term remediation queue: validate hottest queries, confirm index usefulness, and execute corrections before growth amplifies current risk.",
                $"Coloque {tabela} na fila de correção de curto prazo: valide as consultas mais quentes, confirme utilidade dos índices e execute correções antes que o crescimento amplifique o risco atual."),
            _ => M(
                idioma,
                $"Track {tabela} with planned remediation: confirm if the finding affects frequent queries and fix proactively to avoid latent cost escalation.",
                $"Acompanhe {tabela} com correção planejada: confirme se o achado afeta consultas frequentes e corrija de forma preventiva para evitar aumento de custo latente.")
        };
    }

    private static int PesoSeveridade(string severidade)
    {
        return severidade switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            _ => 1
        };
    }

    private static int CalcularScoreRisco(string severidade, string codigo)
    {
        int baseScore = severidade switch
        {
            "critical" => 90,
            "high" => 70,
            "medium" => 45,
            _ => 25
        };

        int ajusteCodigo = codigo switch
        {
            "OPERACIONAL_VOLUME_PRIORIDADE_ALTA" => 10,
            "OPERACIONAL_VOLUME_PRIORIDADE_MEDIA" => 8,
            "OPERACIONAL_VOLUME_PRIORIDADE_BAIXA" => 5,
            "FK_SEM_INDICE_COBERTURA" => 5,
            "INDICE_REDUNDANTE_PREFIXO" => -5,
            "INDICE_DUPLICADO" => -8,
            "FK_DUPLICADA" => -8,
            _ => 0
        };

        int score = baseScore + ajusteCodigo;
        return Math.Max(0, Math.Min(100, score));
    }

    private static string CalcularPrioridade(int scoreRisco)
    {
        if (scoreRisco >= 85) return "P0";
        if (scoreRisco >= 70) return "P1";
        if (scoreRisco >= 45) return "P2";
        return "P3";
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

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }

    private static bool ContemWildcard(string valor)
    {
        return valor.Contains('*') || valor.Contains('?');
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
