using System.Text;
using System.Text.Json;
using System.Globalization;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl;

public static class AnalisadorDdlSchema
{
    public static async Task<(string ArquivoJson, string ArquivoHtml)> AnalisarAsync(OpcoesDdlAnalise opcoes)
    {
        bool portugues = CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(opcoes.Entrada))
            throw new ArgumentException(M(portugues, "Input file not provided (--input).", "Arquivo de entrada nao informado (--input)."));

        string arquivoJsonEntrada = ResolverArquivoJsonSchema(opcoes.Entrada);
        var snapshot = await LerSnapshotAsync(arquivoJsonEntrada);
        var resultado = Analisar(snapshot, portugues, arquivoJsonEntrada);

        var (arquivoJsonSaida, arquivoHtmlSaida) = ResolverArquivosSaida(opcoes);
        Directory.CreateDirectory(Path.GetDirectoryName(arquivoJsonSaida)!);

        await File.WriteAllTextAsync(arquivoJsonSaida, JsonSerializer.Serialize(resultado, JsonOptions));
        await File.WriteAllTextAsync(arquivoHtmlSaida, MontarHtml(resultado, portugues));

        return (arquivoJsonSaida, arquivoHtmlSaida);
    }

    public static ResultadoAnaliseDdl Analisar(
        SnapshotSchema snapshot,
        bool portugues = false,
        string? origem = null)
    {
        var resultado = new ResultadoAnaliseDdl
        {
            Origem = origem ?? string.Empty,
            TotalTabelas = snapshot.Tabelas.Count
        };

        var tabelas = snapshot.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var tabela in snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarTabelaSemColunas(tabela, resultado, portugues);
            ValidarColunasDuplicadas(tabela, resultado, portugues);
            ValidarTiposDesconhecidos(tabela, resultado, portugues);
            ValidarPk(tabela, resultado, portugues);
            ValidarFks(tabela, tabelas, resultado, portugues);
            ValidarIndices(tabela, resultado, portugues);
            ValidarDuplicidadeIndices(tabela, resultado, portugues);
            ValidarDuplicidadeFks(tabela, resultado, portugues);
        }

        resultado.Achados = resultado.Achados
            .OrderByDescending(a => PesoSeveridade(a.Severidade))
            .ThenBy(a => a.Escopo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        resultado.TotalAchados = resultado.Achados.Count;
        resultado.TotalCriticos = resultado.Achados.Count(a => a.Severidade == "critical");
        resultado.TotalAltos = resultado.Achados.Count(a => a.Severidade == "high");
        resultado.TotalMedios = resultado.Achados.Count(a => a.Severidade == "medium");
        resultado.TotalBaixos = resultado.Achados.Count(a => a.Severidade == "low");

        return resultado;
    }

    private static void ValidarTabelaSemColunas(TabelaSchema tabela, ResultadoAnaliseDdl resultado, bool portugues)
    {
        if (tabela.Colunas.Count > 0)
            return;

        AdicionarAchado(
            resultado,
            "critical",
            "TABELA_SEM_COLUNAS",
            tabela.Nome,
            M(portugues, $"Table {tabela.Nome} has no columns.", $"Tabela {tabela.Nome} nao possui colunas."),
            M(portugues, "Re-extract metadata and validate this table directly in system catalogs.", "Reextraia o metadata e valide esta tabela diretamente nos catalogos do Firebird."));
    }

    private static void ValidarColunasDuplicadas(TabelaSchema tabela, ResultadoAnaliseDdl resultado, bool portugues)
    {
        var duplicadas = tabela.Colunas
            .GroupBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        foreach (var coluna in duplicadas)
        {
            AdicionarAchado(
                resultado,
                "critical",
                "COLUNA_DUPLICADA",
                $"{tabela.Nome}.{coluna}",
                M(portugues, $"Duplicated column in table {tabela.Nome}: {coluna}.", $"Coluna duplicada na tabela {tabela.Nome}: {coluna}."),
                M(portugues, "Inspect metadata consistency and rebuild affected objects if needed.", "Inspecione consistencia de metadata e recrie objetos afetados se necessario."));
        }
    }

    private static void ValidarTiposDesconhecidos(TabelaSchema tabela, ResultadoAnaliseDdl resultado, bool portugues)
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
                    portugues,
                    $"Column {tabela.Nome}.{coluna.Nome} has unknown type mapping: {coluna.TipoSql}.",
                    $"Coluna {tabela.Nome}.{coluna.Nome} possui mapeamento de tipo desconhecido: {coluna.TipoSql}."),
                M(portugues, "Validate database version compatibility and inspect field definition in RDB$FIELDS.", "Valide compatibilidade de versao e confira a definicao no RDB$FIELDS."));
        }
    }

    private static void ValidarPk(TabelaSchema tabela, ResultadoAnaliseDdl resultado, bool portugues)
    {
        if (tabela.ChavePrimaria is null)
        {
            AdicionarAchado(
                resultado,
                "medium",
                "TABELA_SEM_PK",
                tabela.Nome,
                M(portugues, $"Table {tabela.Nome} has no primary key.", $"Tabela {tabela.Nome} nao possui chave primaria."),
                M(portugues, "Review if this is expected. Missing PK may hide duplicate rows over time.", "Revise se isso e esperado. Ausencia de PK pode mascarar duplicidades."));
            return;
        }

        if (tabela.ChavePrimaria.Colunas.Count == 0)
        {
            AdicionarAchado(
                resultado,
                "critical",
                "PK_SEM_COLUNAS",
                tabela.Nome,
                M(portugues, $"Primary key {tabela.ChavePrimaria.Nome} in {tabela.Nome} has no columns.", $"Chave primaria {tabela.ChavePrimaria.Nome} em {tabela.Nome} nao possui colunas."),
                M(portugues, "Rebuild this PK from validated column metadata.", "Recrie esta PK a partir de metadados validados."));
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
                M(portugues, $"PK {tabela.ChavePrimaria.Nome} references missing column {colunaPk}.", $"PK {tabela.ChavePrimaria.Nome} referencia coluna inexistente {colunaPk}."),
                M(portugues, "Recreate PK and validate relation fields catalog.", "Recrie a PK e valide o catalogo de campos da relacao."));
        }
    }

    private static void ValidarFks(
        TabelaSchema tabela,
        IReadOnlyDictionary<string, TabelaSchema> tabelas,
        ResultadoAnaliseDdl resultado,
        bool portugues)
    {
        var colunasLocais = tabela.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var fk in tabela.ChavesEstrangeiras)
        {
            string escopo = $"{tabela.Nome}.{fk.Nome}";

            if (fk.Colunas.Count == 0 || fk.ColunasReferencia.Count == 0)
            {
                AdicionarAchado(
                    resultado,
                    "critical",
                    "FK_SEM_COLUNAS",
                    escopo,
                    M(portugues, $"FK {fk.Nome} has empty local/reference columns.", $"FK {fk.Nome} possui colunas locais/referencia vazias."),
                    M(portugues, "Recreate FK with explicit and ordered column list.", "Recrie a FK com lista de colunas explicita e ordenada."));
                continue;
            }

            if (fk.Colunas.Count != fk.ColunasReferencia.Count)
            {
                AdicionarAchado(
                    resultado,
                    "critical",
                    "FK_CARDINALIDADE_INVALIDA",
                    escopo,
                    M(portugues, $"FK {fk.Nome} has different local/reference column counts.", $"FK {fk.Nome} possui cardinalidade diferente entre colunas locais e de referencia."),
                    M(portugues, "Recreate FK preserving matching column cardinality.", "Recrie a FK preservando cardinalidade equivalente."));
            }

            foreach (var colunaFk in fk.Colunas)
            {
                if (colunasLocais.ContainsKey(colunaFk))
                    continue;

                AdicionarAchado(
                    resultado,
                    "critical",
                    "FK_COLUNA_LOCAL_INEXISTENTE",
                    escopo,
                    M(portugues, $"FK {fk.Nome} references missing local column {colunaFk}.", $"FK {fk.Nome} referencia coluna local inexistente {colunaFk}."),
                    M(portugues, "Validate relation fields and rebuild FK.", "Valide os campos da relacao e recrie a FK."));
            }

            if (!tabelas.TryGetValue(fk.TabelaReferencia, out var tabelaReferencia))
            {
                AdicionarAchado(
                    resultado,
                    "critical",
                    "FK_TABELA_REFERENCIA_INEXISTENTE",
                    escopo,
                    M(portugues, $"FK {fk.Nome} points to missing table {fk.TabelaReferencia}.", $"FK {fk.Nome} aponta para tabela inexistente {fk.TabelaReferencia}."),
                    M(portugues, "Validate dependency order and metadata integrity for referenced table.", "Valide ordem de dependencia e integridade de metadata da tabela referenciada."));
                continue;
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
                    M(portugues, $"FK {fk.Nome} points to missing referenced column {colunaRef}.", $"FK {fk.Nome} aponta para coluna referenciada inexistente {colunaRef}."),
                    M(portugues, "Recreate FK after validating referenced key definition.", "Recrie a FK apos validar a definicao da chave referenciada."));
            }

            bool possuiIndiceCobertura = tabela.Indices.Any(indice => CobrePrefixo(indice.Colunas, fk.Colunas));
            if (!possuiIndiceCobertura)
            {
                AdicionarAchado(
                    resultado,
                    "medium",
                    "FK_SEM_INDICE_COBERTURA",
                    escopo,
                    M(portugues, $"FK {fk.Nome} has no local covering index.", $"FK {fk.Nome} nao possui indice local de cobertura."),
                    M(portugues, "Create an index for FK columns to reduce lock contention and validation cost.", "Crie indice para as colunas da FK para reduzir contencao e custo de validacao."));
            }
        }
    }

    private static void ValidarIndices(TabelaSchema tabela, ResultadoAnaliseDdl resultado, bool portugues)
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
                    M(portugues, $"Index {indice.Nome} has no columns.", $"Indice {indice.Nome} nao possui colunas."),
                    M(portugues, "Recreate index with explicit column list.", "Recrie o indice com lista explicita de colunas."));
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
                    M(portugues, $"Index {indice.Nome} references missing column {colunaIndice}.", $"Indice {indice.Nome} referencia coluna inexistente {colunaIndice}."),
                    M(portugues, "Recreate index and validate relation fields catalog.", "Recrie o indice e valide o catalogo de campos da relacao."));
            }
        }
    }

    private static void ValidarDuplicidadeIndices(TabelaSchema tabela, ResultadoAnaliseDdl resultado, bool portugues)
    {
        var grupos = tabela.Indices
            .GroupBy(AssinaturaIndice, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var grupo in grupos)
        {
            string nomes = string.Join(", ", grupo.Select(i => i.Nome).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            AdicionarAchado(
                resultado,
                "medium",
                "INDICE_DUPLICADO",
                tabela.Nome,
                M(portugues, $"Duplicated index signature in {tabela.Nome}: {nomes}.", $"Assinatura de indice duplicada em {tabela.Nome}: {nomes}."),
                M(portugues, "Keep only one index per signature after workload validation.", "Mantenha apenas um indice por assinatura apos validar carga de trabalho."));
        }
    }

    private static void ValidarDuplicidadeFks(TabelaSchema tabela, ResultadoAnaliseDdl resultado, bool portugues)
    {
        var grupos = tabela.ChavesEstrangeiras
            .GroupBy(AssinaturaFk, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var grupo in grupos)
        {
            string nomes = string.Join(", ", grupo.Select(f => f.Nome).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            AdicionarAchado(
                resultado,
                "medium",
                "FK_DUPLICADA",
                tabela.Nome,
                M(portugues, $"Duplicated FK signature in {tabela.Nome}: {nomes}.", $"Assinatura de FK duplicada em {tabela.Nome}: {nomes}."),
                M(portugues, "Consolidate equivalent foreign keys and keep only one validated constraint.", "Consolide FKs equivalentes e mantenha apenas uma restricao validada."));
        }
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
        string recomendacao)
    {
        resultado.Achados.Add(new AchadoAnaliseDdl
        {
            Severidade = severidade,
            Codigo = codigo,
            Escopo = escopo,
            Descricao = descricao,
            Recomendacao = recomendacao
        });
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

    private static async Task<SnapshotSchema> LerSnapshotAsync(string arquivoJson)
    {
        if (!File.Exists(arquivoJson))
            throw new FileNotFoundException($"Arquivo de schema nao encontrado: {arquivoJson}");

        string texto = await File.ReadAllTextAsync(arquivoJson);
        var snapshot = JsonSerializer.Deserialize<SnapshotSchema>(texto, JsonOptions);
        if (snapshot is null)
            throw new ArgumentException($"Nao foi possivel ler snapshot JSON: {arquivoJson}");

        return snapshot;
    }

    private static string ResolverArquivoJsonSchema(string caminhoInformado)
    {
        string caminho = Path.GetFullPath(caminhoInformado.Trim().Trim('"'));

        if (Path.GetExtension(caminho).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return caminho;

        if (Path.GetExtension(caminho).Equals(".sql", StringComparison.OrdinalIgnoreCase))
            return Path.ChangeExtension(caminho, ".schema.json");

        return $"{caminho}.schema.json";
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

    private static string MontarHtml(ResultadoAnaliseDdl resultado, bool portugues)
    {
        string titulo = M(portugues, "DDL Risk Analysis", "Analise de Risco DDL");
        string semAchados = M(portugues, "No findings.", "Sem achados.");
        string origem = M(portugues, "Source", "Origem");
        string geradoEm = M(portugues, "Generated at (UTC)", "Gerado em (UTC)");
        string total = M(portugues, "Total findings", "Total de achados");
        string criticos = M(portugues, "Critical", "Criticos");
        string altos = M(portugues, "High", "Altos");
        string medios = M(portugues, "Medium", "Medios");
        string baixos = M(portugues, "Low", "Baixos");
        string colSeveridade = M(portugues, "Severity", "Severidade");
        string colCodigo = M(portugues, "Code", "Codigo");
        string colEscopo = M(portugues, "Scope", "Escopo");
        string colDescricao = M(portugues, "Description", "Descricao");
        string colRecomendacao = M(portugues, "Recommendation", "Recomendacao");

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine($"<html lang=\"{(portugues ? "pt-BR" : "en")}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine($"  <title>{Html(titulo)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; }");
        sb.AppendLine("    h1 { margin: 0 0 16px 0; }");
        sb.AppendLine("    .meta { margin-bottom: 16px; font-size: 14px; }");
        sb.AppendLine("    .kpi { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 16px; }");
        sb.AppendLine("    .pill { border: 1px solid #d1d5db; border-radius: 12px; padding: 8px 12px; background: #f9fafb; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; }");
        sb.AppendLine("    th, td { border: 1px solid #e5e7eb; padding: 8px; text-align: left; vertical-align: top; font-size: 13px; }");
        sb.AppendLine("    th { background: #f3f4f6; }");
        sb.AppendLine("    .critical { color: #991b1b; font-weight: 700; }");
        sb.AppendLine("    .high { color: #92400e; font-weight: 700; }");
        sb.AppendLine("    .medium { color: #1d4ed8; font-weight: 700; }");
        sb.AppendLine("    .low { color: #065f46; font-weight: 700; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"  <h1>{Html(titulo)}</h1>");
        sb.AppendLine("  <div class=\"meta\">");
        sb.AppendLine($"    <div><strong>{Html(origem)}:</strong> {Html(resultado.Origem)}</div>");
        sb.AppendLine($"    <div><strong>{Html(geradoEm)}:</strong> {resultado.GeradoEmUtc:yyyy-MM-dd HH:mm:ss}</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"kpi\">");
        sb.AppendLine($"    <div class=\"pill\"><strong>{Html(total)}:</strong> {resultado.TotalAchados}</div>");
        sb.AppendLine($"    <div class=\"pill\"><strong>{Html(criticos)}:</strong> {resultado.TotalCriticos}</div>");
        sb.AppendLine($"    <div class=\"pill\"><strong>{Html(altos)}:</strong> {resultado.TotalAltos}</div>");
        sb.AppendLine($"    <div class=\"pill\"><strong>{Html(medios)}:</strong> {resultado.TotalMedios}</div>");
        sb.AppendLine($"    <div class=\"pill\"><strong>{Html(baixos)}:</strong> {resultado.TotalBaixos}</div>");
        sb.AppendLine("  </div>");

        if (resultado.Achados.Count == 0)
        {
            sb.AppendLine($"  <p>{Html(semAchados)}</p>");
        }
        else
        {
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead>");
            sb.AppendLine("      <tr>");
            sb.AppendLine($"        <th>{Html(colSeveridade)}</th>");
            sb.AppendLine($"        <th>{Html(colCodigo)}</th>");
            sb.AppendLine($"        <th>{Html(colEscopo)}</th>");
            sb.AppendLine($"        <th>{Html(colDescricao)}</th>");
            sb.AppendLine($"        <th>{Html(colRecomendacao)}</th>");
            sb.AppendLine("      </tr>");
            sb.AppendLine("    </thead>");
            sb.AppendLine("    <tbody>");

            foreach (var achado in resultado.Achados)
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td class=\"{achado.Severidade}\">{Html(achado.Severidade)}</td>");
                sb.AppendLine($"        <td>{Html(achado.Codigo)}</td>");
                sb.AppendLine($"        <td>{Html(achado.Escopo)}</td>");
                sb.AppendLine($"        <td>{Html(achado.Descricao)}</td>");
                sb.AppendLine($"        <td>{Html(achado.Recomendacao)}</td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string Html(string valor)
    {
        return System.Net.WebUtility.HtmlEncode(valor ?? string.Empty);
    }

    private static string M(bool portugues, string english, string portuguese)
    {
        return portugues ? portuguese : english;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
