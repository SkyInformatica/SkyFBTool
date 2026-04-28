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
            ValidarDuplicidadeFks(tabela, resultado, idioma, severidadesOverride);
        }

        resultado.Achados = resultado.Achados
            .OrderByDescending(a => PesoSeveridade(a.Severidade))
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
        bool possuiIndiceCobertura = tabela.Indices.Any(indice => CobrePrefixo(indice.Colunas, fk.Colunas));
        if (possuiIndiceCobertura)
            return;

        AdicionarAchado(
            resultado,
            "medium",
            "FK_SEM_INDICE_COBERTURA",
            escopo,
            M(idioma, $"FK {fk.Nome} has no local covering index.", $"FK {fk.Nome} não possui índice local de cobertura."),
            M(idioma, "Create an index for FK columns to reduce lock contention and validation cost.", "Crie índice para as colunas da FK para reduzir contenção e custo de validação."),
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
            AdicionarAchado(
                resultado,
                "low",
                "INDICE_DUPLICADO",
                tabela.Nome,
                M(idioma, $"Duplicated index signature in {tabela.Nome}: {nomes}.", $"Assinatura de índice duplicada em {tabela.Nome}: {nomes}."),
                M(idioma, "Keep only one index per signature after workload validation.", "Mantenha apenas um índice por assinatura após validar carga de trabalho."),
                severidadesOverride);
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

        resultado.Achados.Add(new AchadoAnaliseDdl
        {
            Severidade = severidadeFinal,
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
        List<AchadoOperacionalDdl> achadosOperacionais;

        try
        {
            achadosOperacionais = await AnalisadorOperacionalFirebird.ColetarAchadosAsync(opcoes, idioma);
        }
        catch
        {
            return;
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

        resultado.Achados = resultado.Achados
            .OrderByDescending(a => PesoSeveridade(a.Severidade))
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
