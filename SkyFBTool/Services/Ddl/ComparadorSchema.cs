using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl;

public static class ComparadorSchema
{
    public static async Task<(string ArquivoSql, string ArquivoJson, string ArquivoHtml)> CompararAsync(OpcoesDdlDiff opcoes)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();

        if (string.IsNullOrWhiteSpace(opcoes.Origem))
            throw new ArgumentException(M(idioma, "Source file not provided (--source).", "Arquivo de origem nao informado (--source)."));
        if (string.IsNullOrWhiteSpace(opcoes.Alvo))
            throw new ArgumentException(M(idioma, "Target file not provided (--target).", "Arquivo de alvo nao informado (--target)."));

        string origemJson = ResolverArquivoJsonSchema(opcoes.Origem);
        string alvoJson = ResolverArquivoJsonSchema(opcoes.Alvo);

        var origem = await LerSnapshotAsync(origemJson);
        var alvo = await LerSnapshotAsync(alvoJson);

        var resultado = GerarDiff(origem, alvo, idioma);
        var (arquivoSql, arquivoJson, arquivoHtml) = ResolverArquivosSaida(opcoes);

        Directory.CreateDirectory(Path.GetDirectoryName(arquivoSql)!);
        await File.WriteAllTextAsync(arquivoSql, MontarSql(resultado, idioma));
        await File.WriteAllTextAsync(arquivoJson, JsonSerializer.Serialize(resultado, JsonOptions));
        await File.WriteAllTextAsync(arquivoHtml, MontarHtml(resultado, origemJson, alvoJson, origem, alvo, idioma));

        return (arquivoSql, arquivoJson, arquivoHtml);
    }

    public static ResultadoDiffSchema GerarDiff(
        SnapshotSchema origem,
        SnapshotSchema alvo,
        IdiomaSaida idioma = IdiomaSaida.English)
    {
        var resultado = new ResultadoDiffSchema();

        var tabelasOrigem = origem.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);
        var tabelasAlvo = alvo.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var nomeTabela in tabelasOrigem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var tabelaOrigem = tabelasOrigem[nomeTabela];
            if (!tabelasAlvo.TryGetValue(nomeTabela, out var tabelaAlvo))
            {
                resultado.ItensCriados.Add(M(
                    idioma,
                    $"Table missing in target: {nomeTabela}",
                    $"Tabela ausente no alvo: {nomeTabela}"));
                resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateTable(tabelaOrigem));

                if (tabelaOrigem.ChavePrimaria is not null)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddPk(tabelaOrigem));
                foreach (var fk in tabelaOrigem.ChavesEstrangeiras)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddFk(tabelaOrigem, fk));
                foreach (var indice in tabelaOrigem.Indices)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateIndex(tabelaOrigem, indice));

                continue;
            }

            CompararTabela(tabelaOrigem, tabelaAlvo, resultado, idioma);
        }

        foreach (var nomeTabelaAlvo in tabelasAlvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!tabelasOrigem.ContainsKey(nomeTabelaAlvo))
            {
                resultado.ItensSomenteNoAlvo.Add(M(
                    idioma,
                    $"Table exists only in target: {nomeTabelaAlvo}",
                    $"Tabela existe apenas no alvo: {nomeTabelaAlvo}"));
            }
        }

        return resultado;
    }

    private static void CompararTabela(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var colunasOrigem = origem.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        var colunasAlvo = alvo.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var nomeColuna in colunasOrigem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var colunaOrigem = colunasOrigem[nomeColuna];
            if (!colunasAlvo.TryGetValue(nomeColuna, out var colunaAlvo))
            {
                resultado.ItensCriados.Add(M(
                    idioma,
                    $"Column missing in target: {origem.Nome}.{nomeColuna}",
                    $"Coluna ausente no alvo: {origem.Nome}.{nomeColuna}"));
                resultado.ComandosSql.Add(
                    $"ALTER TABLE {GeradorDdlSql.Q(origem.Nome)} ADD {GeradorDdlSql.GerarDefinicaoColuna(colunaOrigem)};");
                continue;
            }

            CompararColuna(origem.Nome, colunaOrigem, colunaAlvo, resultado, idioma);
        }

        foreach (var nomeColunaAlvo in colunasAlvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!colunasOrigem.ContainsKey(nomeColunaAlvo))
            {
                resultado.ItensSomenteNoAlvo.Add(M(
                    idioma,
                    $"Column exists only in target: {origem.Nome}.{nomeColunaAlvo}",
                    $"Coluna existe apenas no alvo: {origem.Nome}.{nomeColunaAlvo}"));
            }
        }

        CompararPk(origem, alvo, resultado, idioma);
        CompararFks(origem, alvo, resultado, idioma);
        CompararIndices(origem, alvo, resultado, idioma);
    }

    private static void CompararColuna(
        string nomeTabela,
        ColunaSchema origem,
        ColunaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        if (!string.Equals(origem.ComputedBySql, alvo.ComputedBySql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.Avisos.Add(M(
                idioma,
                $"Computed column differs: {nomeTabela}.{origem.Nome} (manual adjustment recommended).",
                $"Coluna computada diferente: {nomeTabela}.{origem.Nome} (ajuste manual recomendado)."));
            return;
        }

        if (!string.Equals(origem.TipoSql, alvo.TipoSql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.ItensAlterados.Add(M(
                idioma,
                $"Type differs: {nomeTabela}.{origem.Nome} ({alvo.TipoSql} -> {origem.TipoSql})",
                $"Tipo diferente: {nomeTabela}.{origem.Nome} ({alvo.TipoSql} -> {origem.TipoSql})"));
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} TYPE {origem.TipoSql};");
        }

        if (origem.AceitaNulo != alvo.AceitaNulo)
        {
            string nulidadeOrigem = origem.AceitaNulo ? "NULL" : "NOT NULL";
            string nulidadeAlvo = alvo.AceitaNulo ? "NULL" : "NOT NULL";
            resultado.ItensAlterados.Add(M(
                idioma,
                $"Nullability differs: {nomeTabela}.{origem.Nome} (target: {nulidadeAlvo} -> source: {nulidadeOrigem})",
                $"Nulidade diferente: {nomeTabela}.{origem.Nome} (alvo: {nulidadeAlvo} -> origem: {nulidadeOrigem})"));
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} {(origem.AceitaNulo ? "DROP" : "SET")} NOT NULL;");
        }

        if (!string.Equals(origem.DefaultSql, alvo.DefaultSql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.ItensAlterados.Add(M(
                idioma,
                $"Default differs: {nomeTabela}.{origem.Nome}",
                $"Default diferente: {nomeTabela}.{origem.Nome}"));
            if (string.IsNullOrWhiteSpace(origem.DefaultSql))
            {
                resultado.ComandosSql.Add(
                    $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} DROP DEFAULT;");
            }
            else
            {
                resultado.ComandosSql.Add(
                    $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} SET {origem.DefaultSql};");
            }
        }
    }

    private static void CompararPk(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        string assinaturaOrigem = AssinaturaPk(origem.ChavePrimaria);
        string assinaturaAlvo = AssinaturaPk(alvo.ChavePrimaria);
        if (assinaturaOrigem == assinaturaAlvo)
            return;

        string nomePkOrigem = origem.ChavePrimaria?.Nome ?? M(idioma, "none", "nenhuma");
        string nomePkAlvo = alvo.ChavePrimaria?.Nome ?? M(idioma, "none", "nenhuma");
        string colunasOrigem = origem.ChavePrimaria is null
            ? M(idioma, "none", "nenhuma")
            : string.Join(", ", origem.ChavePrimaria.Colunas);
        string colunasAlvo = alvo.ChavePrimaria is null
            ? M(idioma, "none", "nenhuma")
            : string.Join(", ", alvo.ChavePrimaria.Colunas);
        resultado.ItensAlterados.Add(M(
            idioma,
            $"PK differs: {origem.Nome} (target: {nomePkAlvo} [{colunasAlvo}] -> source: {nomePkOrigem} [{colunasOrigem}])",
            $"PK diferente: {origem.Nome} (alvo: {nomePkAlvo} [{colunasAlvo}] -> origem: {nomePkOrigem} [{colunasOrigem}])"));
        if (alvo.ChavePrimaria is not null)
        {
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(origem.Nome)} DROP CONSTRAINT {GeradorDdlSql.Q(alvo.ChavePrimaria.Nome)};");
        }

        if (origem.ChavePrimaria is not null)
            resultado.ComandosSql.Add(GeradorDdlSql.GerarAddPk(origem));
    }

    private static void CompararFks(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var fksOrigem = CriarMapaPorAssinatura(
            origem.ChavesEstrangeiras,
            AssinaturaFk,
            fk => fk.Nome,
            resultado,
            M(idioma, $"Duplicated FK in source ({origem.Nome})", $"FK duplicada na origem ({origem.Nome})"));
        var fksAlvo = CriarMapaPorAssinatura(
            alvo.ChavesEstrangeiras,
            AssinaturaFk,
            fk => fk.Nome,
            resultado,
            M(idioma, $"Duplicated FK in target ({alvo.Nome})", $"FK duplicada no alvo ({alvo.Nome})"));

        foreach (var assinatura in fksOrigem.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (fksAlvo.ContainsKey(assinatura))
                continue;

            resultado.ItensCriados.Add(M(
                idioma,
                $"FK missing in target: {origem.Nome}.{fksOrigem[assinatura].Nome}",
                $"FK ausente no alvo: {origem.Nome}.{fksOrigem[assinatura].Nome}"));
            resultado.ComandosSql.Add(GeradorDdlSql.GerarAddFk(origem, fksOrigem[assinatura]));
        }

        foreach (var assinatura in fksAlvo.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (!fksOrigem.ContainsKey(assinatura))
            {
                resultado.ItensSomenteNoAlvo.Add(M(
                    idioma,
                    $"FK exists only in target: {origem.Nome}.{fksAlvo[assinatura].Nome}",
                    $"FK existe apenas no alvo: {origem.Nome}.{fksAlvo[assinatura].Nome}"));
            }
        }
    }

    private static void CompararIndices(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var idxOrigem = CriarMapaPorAssinatura(
            origem.Indices,
            AssinaturaIndice,
            idx => idx.Nome,
            resultado,
            M(idioma, $"Duplicated index in source ({origem.Nome})", $"Indice duplicado na origem ({origem.Nome})"));
        var idxAlvo = CriarMapaPorAssinatura(
            alvo.Indices,
            AssinaturaIndice,
            idx => idx.Nome,
            resultado,
            M(idioma, $"Duplicated index in target ({alvo.Nome})", $"Indice duplicado no alvo ({alvo.Nome})"));

        foreach (var assinatura in idxOrigem.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (idxAlvo.ContainsKey(assinatura))
                continue;

            resultado.ItensCriados.Add(M(
                idioma,
                $"Index missing in target: {origem.Nome}.{idxOrigem[assinatura].Nome}",
                $"Indice ausente no alvo: {origem.Nome}.{idxOrigem[assinatura].Nome}"));
            resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateIndex(origem, idxOrigem[assinatura]));
        }

        foreach (var assinatura in idxAlvo.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (!idxOrigem.ContainsKey(assinatura))
            {
                resultado.ItensSomenteNoAlvo.Add(M(
                    idioma,
                    $"Index exists only in target: {origem.Nome}.{idxAlvo[assinatura].Nome}",
                    $"Indice existe apenas no alvo: {origem.Nome}.{idxAlvo[assinatura].Nome}"));
            }
        }
    }

    private static string AssinaturaPk(ChavePrimariaSchema? pk)
    {
        if (pk is null)
            return string.Empty;
        return string.Join("|", pk.Colunas).ToUpperInvariant();
    }

    private static string AssinaturaFk(ChaveEstrangeiraSchema fk)
    {
        return string.Join("|",
            fk.Colunas).ToUpperInvariant() +
               "->" + fk.TabelaReferencia.ToUpperInvariant() +
               "(" + string.Join("|", fk.ColunasReferencia).ToUpperInvariant() + ")" +
               $"[{fk.RegraUpdate.ToUpperInvariant()}|{fk.RegraDelete.ToUpperInvariant()}]";
    }

    private static string AssinaturaIndice(IndiceSchema idx)
    {
        return $"{(idx.Unico ? "U" : "N")}|{(idx.Descendente ? "D" : "A")}|{string.Join("|", idx.Colunas).ToUpperInvariant()}";
    }

    private static Dictionary<string, T> CriarMapaPorAssinatura<T>(
        IEnumerable<T> itens,
        Func<T, string> assinatura,
        Func<T, string>? nome,
        ResultadoDiffSchema resultado,
        string contextoAviso)
    {
        var mapa = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in itens)
        {
            string chave = assinatura(item);
            if (mapa.ContainsKey(chave))
            {
                string nomeAtual = NomeItem(nome, item);
                string nomeExistente = NomeItem(nome, mapa[chave]);
                resultado.Avisos.Add($"{contextoAviso}: {nomeExistente} <-> {nomeAtual} | {chave}");
                continue;
            }

            mapa.Add(chave, item);
        }

        return mapa;
    }

    private static string NomeItem<T>(Func<T, string>? resolverNome, T item)
    {
        if (resolverNome is null)
            return "?";

        string nome = resolverNome(item) ?? string.Empty;
        return string.IsNullOrWhiteSpace(nome) ? "?" : nome.Trim();
    }

    private static async Task<SnapshotSchema> LerSnapshotAsync(string arquivoJson)
    {
        if (!File.Exists(arquivoJson))
            throw new FileNotFoundException($"Arquivo de schema nao encontrado: {arquivoJson}");

        var texto = await File.ReadAllTextAsync(arquivoJson);
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

    private static (string ArquivoSql, string ArquivoJson, string ArquivoHtml) ResolverArquivosSaida(OpcoesDdlDiff opcoes)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string padrao = $"schema_diff_{timestamp}";

        if (string.IsNullOrWhiteSpace(opcoes.Saida))
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), padrao);
            return ($"{basePath}.sql", $"{basePath}.json", $"{basePath}.html");
        }

        string saida = opcoes.Saida.Trim();
        if (Directory.Exists(saida) || saida.EndsWith(Path.DirectorySeparatorChar) || saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string basePath = Path.Combine(Path.GetFullPath(saida), padrao);
            return ($"{basePath}.sql", $"{basePath}.json", $"{basePath}.html");
        }

        string semExtensao = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(saida)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(saida));

        return ($"{semExtensao}.sql", $"{semExtensao}.json", $"{semExtensao}.html");
    }

    private static string MontarSql(ResultadoDiffSchema resultado, IdiomaSaida idioma)
    {
        if (resultado.ComandosSql.Count == 0)
        {
            return M(
                       idioma,
                       "-- No differences requiring automatic SQL generation.",
                       "-- Nenhuma diferenca que exija SQL automatico.")
                   + Environment.NewLine;
        }

        return string.Join(Environment.NewLine, resultado.ComandosSql) + Environment.NewLine;
    }

    private static string MontarHtml(
        ResultadoDiffSchema resultado,
        string origemJson,
        string alvoJson,
        SnapshotSchema origem,
        SnapshotSchema alvo,
        IdiomaSaida idioma)
    {
        string titulo = M(idioma, "DDL Diff Report", "Relatório DDL Diff");
        string fonte = M(idioma, "Source", "Origem");
        string alvoLabel = M(idioma, "Target", "Alvo");
        string resumo = M(idioma, "Summary", "Resumo");
        string comandos = M(idioma, "Generated SQL commands", "Comandos SQL gerados");
        string criados = M(idioma, "Created items", "Itens criados");
        string alterados = M(idioma, "Changed items", "Itens alterados");
        string somenteAlvo = M(idioma, "Items only in target", "Itens somente no alvo");
        string avisos = M(idioma, "Warnings", "Avisos");
        string nenhum = M(idioma, "None", "Nenhum");
        string secaoSintese = M(idioma, "Executive Summary (Target Focus)", "Síntese Executiva (Foco no Alvo)");
        string secaoDetalhe = M(idioma, "Detailed Findings", "Achados Detalhados");

        var metricaOrigem = CalcularMetricas(origem);
        var metricaAlvo = CalcularMetricas(alvo);
        var acoesAlvo = MontarAcoesAlvo(resultado, idioma);
        var topCriticos = MontarTopCriticosAlvo(resultado, idioma);
        var blocosExecucao = MontarBlocosExecucaoSql(resultado.ComandosSql, idioma);
        var checklist = MontarChecklistPosAplicacao(idioma);

        string html = $$"""
<!doctype html>
<html lang="{{(idioma == IdiomaSaida.PortugueseBrazil ? "pt-BR" : "en")}}">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{Html(titulo)}}</title>
  <style>
    :root {
      --bg: #f6f8fb;
      --panel: #ffffff;
      --line: #d9e1ee;
      --text: #132033;
      --muted: #5a6a82;
      --good: #1f7a4f;
      --warn: #946500;
      --accent: #0f5ec9;
      --radius: 8px;
    }
    @media (prefers-color-scheme: dark) {
      :root {
        --bg: #0d121b;
        --panel: #121a26;
        --line: #2a3648;
        --text: #e7edf9;
        --muted: #9db0cf;
        --good: #5fd2a0;
        --warn: #f4c56a;
        --accent: #66a5ff;
      }
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      background: var(--bg);
      color: var(--text);
      font-family: Segoe UI, Inter, Arial, sans-serif;
      line-height: 1.45;
    }
    .wrap {
      max-width: 1180px;
      margin: 18px auto 28px auto;
      padding: 0 14px;
    }
    .head {
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: var(--radius);
      padding: 16px;
      margin-bottom: 14px;
    }
    h1, h2 { margin: 0; }
    h1 { font-size: 24px; margin-bottom: 10px; }
    h2 { font-size: 18px; margin-bottom: 10px; }
    .meta-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
    }
    .meta-item {
      background: color-mix(in srgb, var(--panel), var(--bg) 24%);
      border: 1px solid var(--line);
      border-radius: 6px;
      padding: 9px 10px;
      font-size: 13px;
    }
    .label { color: var(--muted); font-weight: 600; display: block; margin-bottom: 5px; font-size: 12px; }
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
      gap: 10px;
      margin: 12px 0 14px 0;
    }
    .card {
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: var(--radius);
      padding: 12px;
    }
    .card .k { font-size: 12px; color: var(--muted); }
    .card .v { font-size: 24px; font-weight: 700; margin-top: 4px; }
    .v.warn { color: var(--warn); }
    .v.good { color: var(--good); }
    .section {
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: var(--radius);
      padding: 14px;
      margin-top: 12px;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    th, td {
      text-align: left;
      border-bottom: 1px solid var(--line);
      padding: 8px;
      vertical-align: top;
    }
    th { color: var(--muted); font-weight: 600; font-size: 12px; }
    .nowrap { white-space: nowrap; }
    .muted { color: var(--muted); }
    .chip {
      display: inline-block;
      border-radius: 999px;
      padding: 2px 8px;
      font-size: 11px;
      border: 1px solid var(--line);
      color: var(--muted);
    }
    .chip.warn { color: var(--warn); border-color: color-mix(in srgb, var(--warn), var(--line) 70%); }
    .chip.good { color: var(--good); border-color: color-mix(in srgb, var(--good), var(--line) 70%); }
    .warn-toolbar {
      display: grid;
      grid-template-columns: 1.6fr .9fr;
      gap: 8px;
      margin-bottom: 10px;
      align-items: start;
    }
    .warn-field {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .warn-toolbar .label {
      margin-bottom: 0;
    }
    .warn-input, .warn-select {
      background: var(--panel);
      color: var(--text);
      border: 1px solid var(--line);
      border-radius: 6px;
      padding: 7px 9px;
      font-size: 12px;
      width: 100%;
    }
    .def-box {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 220px;
      max-width: 360px;
    }
    .def-item {
      display: flex;
      align-items: baseline;
      gap: 6px;
      flex-wrap: wrap;
      font-size: 12px;
      line-height: 1.35;
    }
    .def-key {
      color: var(--muted);
      font-weight: 600;
      white-space: nowrap;
    }
    .def-val {
      word-break: break-word;
      white-space: normal;
    }
    details.fold { border-top: 1px dashed var(--line); padding-top: 8px; margin-top: 8px; }
    details.fold summary { cursor: pointer; list-style: none; font-weight: 600; color: var(--muted); }
    details.fold summary::-webkit-details-marker { display: none; }
    details.fold summary::before { content: "▶ "; display: inline-block; margin-right: 4px; }
    details.fold[open] summary::before { content: "▼ "; }
    code {
      font-family: Consolas, "Cascadia Mono", monospace;
      font-size: 12px;
      word-break: break-all;
    }
    @media (max-width: 860px) {
      .meta-grid { grid-template-columns: 1fr; }
      th:nth-child(3), td:nth-child(3) { display: none; }
      .warn-toolbar { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
  <div class="wrap">
    <div class="head">
      <h1>{{Html(titulo)}}</h1>
      <div class="meta-grid">
        <div class="meta-item">
          <span class="label">{{Html(fonte)}}</span>
          <code>{{Html(origemJson)}}</code>
        </div>
        <div class="meta-item">
          <span class="label">{{Html(alvoLabel)}}</span>
          <code>{{Html(alvoJson)}}</code>
        </div>
      </div>
    </div>

    <div class="section">
      <h2>{{Html(secaoSintese)}}</h2>
      <div class="muted">{{Html(resumo)}}</div>
      <div class="cards">
        <div class="card"><div class="k">{{Html(comandos)}}</div><div class="v">{{resultado.ComandosSql.Count}}</div></div>
        <div class="card"><div class="k">{{Html(criados)}}</div><div class="v good">{{resultado.ItensCriados.Count}}</div></div>
        <div class="card"><div class="k">{{Html(alterados)}}</div><div class="v">{{resultado.ItensAlterados.Count}}</div></div>
        <div class="card"><div class="k">{{Html(somenteAlvo)}}</div><div class="v">{{resultado.ItensSomenteNoAlvo.Count}}</div></div>
        <div class="card"><div class="k">{{Html(avisos)}}</div><div class="v warn">{{resultado.Avisos.Count}}</div></div>
      </div>
    </div>

    <div class="section">
      <h2>{{Html(M(idioma, "Schema Comparison", "Comparação de Schema"))}}</h2>
      <table>
        <thead>
          <tr>
            <th class="nowrap">{{Html(M(idioma, "Metric", "Métrica"))}}</th>
            <th class="nowrap">{{Html(fonte)}}</th>
            <th class="nowrap">{{Html(alvoLabel)}}</th>
            <th class="nowrap">Δ</th>
          </tr>
        </thead>
        <tbody>
          <tr><td>{{Html(M(idioma, "Tables", "Tabelas"))}}</td><td>{{metricaOrigem.TotalTabelas}}</td><td>{{metricaAlvo.TotalTabelas}}</td><td>{{metricaOrigem.TotalTabelas - metricaAlvo.TotalTabelas}}</td></tr>
          <tr><td>{{Html(M(idioma, "Columns", "Colunas"))}}</td><td>{{metricaOrigem.TotalColunas}}</td><td>{{metricaAlvo.TotalColunas}}</td><td>{{metricaOrigem.TotalColunas - metricaAlvo.TotalColunas}}</td></tr>
          <tr><td>{{Html(M(idioma, "Primary Keys", "Chaves Primárias"))}}</td><td>{{metricaOrigem.TotalPks}}</td><td>{{metricaAlvo.TotalPks}}</td><td>{{metricaOrigem.TotalPks - metricaAlvo.TotalPks}}</td></tr>
          <tr><td>{{Html(M(idioma, "Foreign Keys", "Chaves Estrangeiras"))}}</td><td>{{metricaOrigem.TotalFks}}</td><td>{{metricaAlvo.TotalFks}}</td><td>{{metricaOrigem.TotalFks - metricaAlvo.TotalFks}}</td></tr>
          <tr><td>{{Html(M(idioma, "Indexes", "Índices"))}}</td><td>{{metricaOrigem.TotalIndices}}</td><td>{{metricaAlvo.TotalIndices}}</td><td>{{metricaOrigem.TotalIndices - metricaAlvo.TotalIndices}}</td></tr>
        </tbody>
      </table>
    </div>

    {{SecaoAvisosHtml(avisos, resultado.Avisos, nenhum, idioma)}}
    {{SecaoAcoesAlvoHtml(acoesAlvo, idioma)}}
    {{SecaoTopCriticosHtml(topCriticos, idioma)}}
    {{SecaoBlocosExecucaoHtml(blocosExecucao, idioma)}}
    {{SecaoChecklistHtml(checklist, idioma)}}

    <div class="section">
      <h2>{{Html(secaoDetalhe)}}</h2>
      <div class="muted">{{Html(M(idioma, "Technical detail for execution planning.", "Detalhamento tecnico para planejamento de execucao."))}}</div>
    </div>

    {{SecaoTabelaHtml(criados, resultado.ItensCriados, nenhum, "good", idioma, false)}}
    {{SecaoTabelaHtml(alterados, resultado.ItensAlterados, nenhum, "", idioma, false)}}
    {{SecaoTabelaHtml(somenteAlvo, resultado.ItensSomenteNoAlvo, nenhum, "", idioma, true)}}
  </div>
  <script>
    (function inicializarFiltrosAviso() {
      const rows = Array.from(document.querySelectorAll('.warn-row'));
      const typeSelect = document.getElementById('warn-filter-type');
      if (!rows.length || !typeSelect) return;

      const tipos = [...new Set(rows.map(r => (r.dataset.type || '').trim()).filter(Boolean))].sort();

      for (const t of tipos) {
        const o = document.createElement('option');
        o.value = t;
        o.textContent = t;
        typeSelect.appendChild(o);
      }
    })();

    function filtrarAvisos() {
      const text = (document.getElementById('warn-filter-text')?.value || '').toLowerCase();
      const type = document.getElementById('warn-filter-type')?.value || '';
      const rows = Array.from(document.querySelectorAll('.warn-row'));

      for (const row of rows) {
        const hay = (row.dataset.search || '').toLowerCase();
        const rowType = row.dataset.type || '';
        const okText = !text || hay.includes(text);
        const okType = !type || rowType === type;
        row.style.display = (okText && okType) ? '' : 'none';
      }
    }
  </script>
</body>
</html>
""";
        return CorrigirCodificacaoHtml(html);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }

    private static string CorrigirCodificacaoHtml(string html)
    {
        return html
            .Replace("â–¶", "▶", StringComparison.Ordinal)
            .Replace("â–¼", "▼", StringComparison.Ordinal)
            .Replace("Î”", "&Delta;", StringComparison.Ordinal)
            .Replace("ComparaÃ§Ã£o", "Comparação", StringComparison.Ordinal)
            .Replace("MÃ©trica", "Métrica", StringComparison.Ordinal)
            .Replace("PrimÃ¡rias", "Primárias", StringComparison.Ordinal)
            .Replace("Ãndices", "Índices", StringComparison.Ordinal)
            .Replace("Relatorio DDL Diff", "Relatório DDL Diff", StringComparison.Ordinal)
            .Replace("Sintese Executiva", "Síntese Executiva", StringComparison.Ordinal)
            .Replace("Definicao", "Definição", StringComparison.Ordinal)
            .Replace("Acao", "Ação", StringComparison.Ordinal)
            .Replace("Unico", "Único", StringComparison.Ordinal)
            .Replace("Nao", "Não", StringComparison.Ordinal);
    }

    private static string SecaoTabelaHtml(
        string titulo,
        IReadOnlyCollection<string> itens,
        string nenhum,
        string chipClass,
        IdiomaSaida idioma,
        bool colapsadoPorPadrao)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""<div class="section"><h2>{Html(titulo)} <span class="chip {chipClass}">{itens.Count}</span></h2>""");
        sb.AppendLine($"""<details class="fold" {(colapsadoPorPadrao ? "" : "open")}><summary>{Html(M(idioma, "Details", "Detalhes"))}</summary>""");

        if (itens.Count == 0)
        {
            sb.AppendLine($"<div class=\"muted\">{Html(nenhum)}</div></details></div>");
            return sb.ToString();
        }

        sb.AppendLine("<table><thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine($"<th class=\"nowrap\">#</th>");
        sb.AppendLine($"<th>{Html(M(idioma, "Item", "Item"))}</th>");
        sb.AppendLine($"<th class=\"nowrap\">{Html(M(idioma, "Category", "Categoria"))}</th>");
        sb.AppendLine("</tr></thead><tbody>");
        int i = 1;
        foreach (var item in itens.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td class=\"nowrap\">{i}</td>");
            sb.AppendLine($"<td>{Html(item)}</td>");
            sb.AppendLine($"<td class=\"nowrap\"><span class=\"chip {chipClass}\">{Html(titulo)}</span></td>");
            sb.AppendLine("</tr>");
            i++;
        }
        sb.AppendLine("</tbody></table></details></div>");
        return sb.ToString();
    }

    private static string SecaoAvisosHtml(
        string titulo,
        IReadOnlyCollection<string> avisos,
        string nenhum,
        IdiomaSaida idioma)
    {
        var detalhes = avisos
            .Select(a => ParseAviso(a, idioma))
            .OrderByDescending(a => PesoSeveridade(a.Severidade))
            .ThenBy(a => a.Tipo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Escopo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Objetos, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var grupos = detalhes
            .GroupBy(a => string.IsNullOrWhiteSpace(a.Tabela) ? M(idioma, "General", "Geral") : a.Tabela, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"""<div class="section"><h2>{Html(titulo)} <span class="chip warn">{avisos.Count}</span></h2>""");

        if (detalhes.Count == 0)
        {
            sb.AppendLine($"<div class=\"muted\">{Html(nenhum)}</div></div>");
            return sb.ToString();
        }

        string filtroLabel = M(idioma, "Filter warnings", "Filtrar avisos");
        string filtroPlaceholder = M(idioma, "Type table/index/column...", "Digite tabela/indice/coluna...");
        string tipoLabel = M(idioma, "Type", "Tipo");
        string todos = M(idioma, "All", "Todos");

        sb.AppendLine("<div class=\"warn-toolbar\">");
        sb.AppendLine("<div class=\"warn-field\">");
        sb.AppendLine($"<label class=\"label\" for=\"warn-filter-text\">{Html(filtroLabel)}</label>");
        sb.AppendLine($"<input id=\"warn-filter-text\" class=\"warn-input\" type=\"text\" placeholder=\"{Html(filtroPlaceholder)}\" oninput=\"filtrarAvisos()\" />");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"warn-field\">");
        sb.AppendLine($"<label class=\"label\" for=\"warn-filter-type\">{Html(tipoLabel)}</label>");
        sb.AppendLine($"<select id=\"warn-filter-type\" class=\"warn-select\" onchange=\"filtrarAvisos()\"><option value=\"\">{Html(todos)}</option></select>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        foreach (var grupo in grupos)
        {
            bool aberto = grupo.Any(a => a.Severidade == "HIGH");
            sb.AppendLine($"""<details class="fold" {(aberto ? "open" : "")}><summary>{Html(grupo.Key)} <span class="chip">{grupo.Count()}</span></summary>""");
            sb.AppendLine("<table class=\"warn-table\"><thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th class=\"nowrap\">#</th>");
            sb.AppendLine($"<th class=\"nowrap\">{Html(M(idioma, "Severity", "Severidade"))}</th>");
            sb.AppendLine($"<th class=\"nowrap\">{Html(M(idioma, "Type", "Tipo"))}</th>");
            sb.AppendLine($"<th>{Html(M(idioma, "Objects", "Objetos"))}</th>");
            sb.AppendLine($"<th>{Html(M(idioma, "Definition", "Definicao"))}</th>");
            sb.AppendLine($"<th>{Html(M(idioma, "Action", "Acao"))}</th>");
            sb.AppendLine($"<th>{Html(M(idioma, "Details", "Detalhes"))}</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int i = 1;
            foreach (var aviso in grupo)
            {
                string classeSeveridade = aviso.Severidade == "HIGH" ? "warn" : "";
                string detalhesTexto = aviso.Estruturado ? "-" : aviso.Mensagem;
                sb.AppendLine($"""<tr class="warn-row" data-type="{Html(aviso.Tipo)}" data-search="{Html($"{aviso.Tipo} {aviso.Escopo} {aviso.Objetos} {aviso.Definicao} {aviso.Mensagem}")}">""");
                sb.AppendLine($"<td class=\"nowrap\">{i}</td>");
                sb.AppendLine($"<td class=\"nowrap\"><span class=\"chip {classeSeveridade}\">{Html(aviso.Severidade)}</span></td>");
                sb.AppendLine($"<td class=\"nowrap\"><span class=\"chip warn\">{Html(aviso.Tipo)}</span></td>");
                sb.AppendLine($"<td>{RenderObjetos(aviso)}</td>");
                sb.AppendLine($"<td>{RenderDefinicaoHtml(aviso, idioma)}</td>");
                sb.AppendLine($"<td>{Html(aviso.Acao)}</td>");
                sb.AppendLine($"<td>{Html(detalhesTexto)}</td>");
                sb.AppendLine("</tr>");
                i++;
            }

            sb.AppendLine("</tbody></table></details>");
        }

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static AvisoDetalhado ParseAviso(string aviso, IdiomaSaida idioma)
    {
        var duplicidade = ParseAvisoDuplicidade(aviso, idioma);
        if (duplicidade is not null)
            return duplicidade;

        var colunaComputada = ParseAvisoColunaComputada(aviso, idioma);
        if (colunaComputada is not null)
            return colunaComputada;

        return new AvisoDetalhado
        {
            Tipo = M(idioma, "General warning", "Aviso geral"),
            Severidade = "MEDIUM",
            Escopo = "-",
            Tabela = string.Empty,
            Objetos = "-",
            Definicao = "-",
            Acao = M(idioma, "Manual review", "Revisao manual"),
            Estruturado = false,
            Mensagem = aviso
        };
    }

    private static AvisoDetalhado? ParseAvisoColunaComputada(string aviso, IdiomaSaida idioma)
    {
        Match m = Regex.Match(
            aviso,
            @"^(Computed column differs|Coluna computada diferente): (?<obj>[A-Za-z0-9_\$]+\.[A-Za-z0-9_\$]+).*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (!m.Success)
            return null;

        string objeto = m.Groups["obj"].Value.Trim();
        string tabela = objeto.Split('.', 2)[0];
        string escopo = M(idioma, "target", "alvo");

        return new AvisoDetalhado
        {
            Tipo = M(idioma, "Computed column mismatch", "Divergencia em coluna computada"),
            Severidade = "MEDIUM",
            Escopo = $"{escopo}:{tabela}",
            Tabela = tabela,
            Objetos = objeto,
            Definicao = "-",
            Acao = M(idioma, "Adjust expression manually", "Ajustar expressao manualmente"),
            Estruturado = false,
            Mensagem = aviso
        };
    }

    private static AvisoDetalhado? ParseAvisoDuplicidade(string aviso, IdiomaSaida idioma)
    {
        Match m = Regex.Match(
            aviso,
            @"^(?<tipo>.+?) \((?<escopo>[^)]+)\): (?<objA>.+?) <-> (?<objB>.+?) \| (?<sig>.+)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (!m.Success)
            return null;

        string tipoRaw = m.Groups["tipo"].Value.Trim();
        string tabela = m.Groups["escopo"].Value.Trim();
        string escopo = ResolverEscopoAviso(tipoRaw, tabela, idioma);

        string tipo = tipoRaw.Contains("fk", StringComparison.OrdinalIgnoreCase)
            ? "Duplicated FK"
            : "Duplicated index";

        return new AvisoDetalhado
        {
            Tipo = tipo,
            Severidade = "HIGH",
            Escopo = escopo,
            Tabela = tabela,
            ObjetoA = m.Groups["objA"].Value.Trim(),
            ObjetoB = m.Groups["objB"].Value.Trim(),
            Objetos = $"{m.Groups["objA"].Value.Trim()} <> {m.Groups["objB"].Value.Trim()}",
            Definicao = m.Groups["sig"].Value.Trim(),
            Acao = M(idioma, "Keep one / drop duplicate", "Manter um / remover duplicado"),
            Estruturado = true,
            Mensagem = aviso
        };
    }

    private static string ResolverEscopoAviso(string tipoRaw, string tabela, IdiomaSaida idioma)
    {
        bool source = tipoRaw.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                      tipoRaw.Contains("origem", StringComparison.OrdinalIgnoreCase);
        string prefixo = source ? M(idioma, "source", "origem") : M(idioma, "target", "alvo");
        return $"{prefixo}:{tabela}";
    }

    private static string RenderObjetos(AvisoDetalhado aviso)
    {
        if (!string.IsNullOrWhiteSpace(aviso.ObjetoA) && !string.IsNullOrWhiteSpace(aviso.ObjetoB))
        {
            return
                $"<span class=\"chip\">{Html(aviso.ObjetoA)}</span> <span class=\"chip\">{Html(aviso.ObjetoB)}</span>";
        }

        return Html(aviso.Objetos);
    }

    private static string RenderDefinicaoHtml(AvisoDetalhado aviso, IdiomaSaida idioma)
    {
        if (string.IsNullOrWhiteSpace(aviso.Definicao) || aviso.Definicao == "-")
            return "<span class=\"muted\">-</span>";

        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"def-box\">");
        if (aviso.Tipo.Equals("Duplicated index", StringComparison.OrdinalIgnoreCase))
        {
            string[] partes = aviso.Definicao.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (partes.Length >= 3)
            {
                string unico = partes[0].Equals("U", StringComparison.OrdinalIgnoreCase)
                    ? M(idioma, "Yes", "Sim")
                    : M(idioma, "No", "Nao");
                string ordem = partes[1].Equals("D", StringComparison.OrdinalIgnoreCase)
                    ? "DESC"
                    : "ASC";
                string colunas = string.Join(", ", partes.Skip(2));
                AdicionarDefinicaoItem(sb, M(idioma, "Unique", "Unico"), unico);
                AdicionarDefinicaoItem(sb, M(idioma, "Order", "Ordem"), ordem);
                AdicionarDefinicaoItem(sb, M(idioma, "Columns", "Colunas"), colunas);
                sb.AppendLine("</div>");
                return sb.ToString();
            }
        }

        AdicionarDefinicaoItem(sb, M(idioma, "Raw", "Original"), aviso.Definicao);
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static void AdicionarDefinicaoItem(StringBuilder sb, string chave, string valor)
    {
        sb.AppendLine("<div class=\"def-item\">");
        sb.AppendLine($"<span class=\"def-key\">{Html(chave)}:</span>");
        sb.AppendLine($"<span class=\"def-val\">{Html(valor)}</span>");
        sb.AppendLine("</div>");
    }

    private static string SecaoTopCriticosHtml(IReadOnlyList<ItemCriticoAlvo> itens, IdiomaSaida idioma)
    {
        string titulo = M(idioma, "Top 10 critical target items", "Top 10 itens criticos do alvo");
        string nenhum = M(idioma, "None", "Nenhum");
        string severidade = M(idioma, "Severity", "Severidade");
        string itemLabel = M(idioma, "Item", "Item");
        string categoria = M(idioma, "Category", "Categoria");

        var sb = new StringBuilder();
        sb.AppendLine($"""<div class="section"><h2>{Html(titulo)} <span class="chip warn">{itens.Count}</span></h2>""");

        if (itens.Count == 0)
        {
            sb.AppendLine($"<div class=\"muted\">{Html(nenhum)}</div></div>");
            return sb.ToString();
        }

        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine("<th class=\"nowrap\">#</th>");
        sb.AppendLine($"<th class=\"nowrap\">{Html(severidade)}</th>");
        sb.AppendLine($"<th>{Html(itemLabel)}</th>");
        sb.AppendLine($"<th class=\"nowrap\">{Html(categoria)}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        int i = 1;
        foreach (var item in itens)
        {
            string classe = item.Severidade == "HIGH" ? "warn" : "";
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td class=\"nowrap\">{i}</td>");
            sb.AppendLine($"<td class=\"nowrap\"><span class=\"chip {classe}\">{Html(item.Severidade)}</span></td>");
            sb.AppendLine($"<td>{Html(item.Descricao)}</td>");
            sb.AppendLine($"<td class=\"nowrap\">{Html(item.Categoria)}</td>");
            sb.AppendLine("</tr>");
            i++;
        }

        sb.AppendLine("</tbody></table></div>");
        return sb.ToString();
    }

    private static string SecaoBlocosExecucaoHtml(IReadOnlyList<BlocoExecucaoSql> blocos, IdiomaSaida idioma)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""<div class="section"><h2>{Html(M(idioma, "Suggested SQL execution order", "Ordem sugerida de execucao SQL"))}</h2>""");
        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine($"<th class=\"nowrap\">{Html(M(idioma, "Step", "Etapa"))}</th>");
        sb.AppendLine($"<th>{Html(M(idioma, "Block", "Bloco"))}</th>");
        sb.AppendLine($"<th class=\"nowrap\">{Html(M(idioma, "Statements", "Comandos"))}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var bloco in blocos)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td class=\"nowrap\">{bloco.Ordem}</td>");
            sb.AppendLine($"<td>{Html(bloco.Titulo)}</td>");
            sb.AppendLine($"<td class=\"nowrap\">{bloco.Quantidade}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></div>");
        return sb.ToString();
    }

    private static string SecaoChecklistHtml(IReadOnlyList<string> checklist, IdiomaSaida idioma)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""<div class="section"><h2>{Html(M(idioma, "Post-apply checklist", "Checklist pos-aplicacao"))}</h2><ul>""");

        foreach (var item in checklist)
            sb.AppendLine($"<li>{Html(item)}</li>");

        sb.AppendLine("</ul></div>");
        return sb.ToString();
    }

    private static MetricasSchema CalcularMetricas(SnapshotSchema snapshot)
    {
        return new MetricasSchema
        {
            TotalTabelas = snapshot.Tabelas.Count,
            TotalColunas = snapshot.Tabelas.Sum(t => t.Colunas.Count),
            TotalPks = snapshot.Tabelas.Count(t => t.ChavePrimaria is not null),
            TotalFks = snapshot.Tabelas.Sum(t => t.ChavesEstrangeiras.Count),
            TotalIndices = snapshot.Tabelas.Sum(t => t.Indices.Count)
        };
    }

    private sealed class MetricasSchema
    {
        public int TotalTabelas { get; init; }
        public int TotalColunas { get; init; }
        public int TotalPks { get; init; }
        public int TotalFks { get; init; }
        public int TotalIndices { get; init; }
    }

    private static IReadOnlyList<AcaoAlvo> MontarAcoesAlvo(ResultadoDiffSchema resultado, IdiomaSaida idioma)
    {
        int totalTabelaAusente = resultado.ItensCriados.Count(i =>
            i.Contains("Table missing in target", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("Tabela ausente no alvo", StringComparison.OrdinalIgnoreCase));

        int totalEstrutural = resultado.ItensAlterados.Count(i =>
            i.Contains("PK differs", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("Type differs", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("Nullability differs", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("PK diferente", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("Tipo diferente", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("Nulidade diferente", StringComparison.OrdinalIgnoreCase));

        int totalIntegridade = resultado.ItensCriados.Count(i =>
            i.Contains("FK missing", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("FK ausente", StringComparison.OrdinalIgnoreCase));

        int totalPerformance = resultado.ItensCriados.Count(i =>
            i.Contains("Index missing", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("Indice ausente", StringComparison.OrdinalIgnoreCase));

        int totalRevisaoManual = resultado.Avisos.Count + resultado.ItensSomenteNoAlvo.Count;

        var acoes = new List<AcaoAlvo>
        {
            new()
            {
                Prioridade = "P1",
                Tema = M(idioma, "Missing core objects in target", "Objetos principais ausentes no alvo"),
                Quantidade = totalTabelaAusente,
                Recomendacao = M(
                    idioma,
                    "Apply generated CREATE statements first and validate dependencies before opening write traffic.",
                    "Aplicar primeiro os CREATE gerados e validar dependencias antes de liberar escrita.")
            },
            new()
            {
                Prioridade = "P1",
                Tema = M(idioma, "Structural mismatch impacting consistency", "Divergencia estrutural com impacto de consistencia"),
                Quantidade = totalEstrutural,
                Recomendacao = M(
                    idioma,
                    "Execute ALTER TYPE/NOT NULL/PK changes in maintenance window and revalidate application contracts.",
                    "Executar ALTER TYPE/NOT NULL/PK em janela de manutencao e revalidar contratos da aplicacao.")
            },
            new()
            {
                Prioridade = "P2",
                Tema = M(idioma, "Referential integrity not aligned", "Integridade referencial nao alinhada"),
                Quantidade = totalIntegridade,
                Recomendacao = M(
                    idioma,
                    "Prioritize FK synchronization after data cleanup to avoid constraint violations.",
                    "Priorizar sincronizacao de FK apos saneamento de dados para evitar violacao de restricao.")
            },
            new()
            {
                Prioridade = "P2",
                Tema = M(idioma, "Performance objects missing", "Objetos de performance ausentes"),
                Quantidade = totalPerformance,
                Recomendacao = M(
                    idioma,
                    "Create missing indexes after core DDL and monitor execution plans.",
                    "Criar indices ausentes apos o DDL principal e monitorar planos de execucao.")
            },
            new()
            {
                Prioridade = "P3",
                Tema = M(idioma, "Manual review required", "Revisao manual necessaria"),
                Quantidade = totalRevisaoManual,
                Recomendacao = M(
                    idioma,
                    "Review warnings and target-only objects before cleanup or drop actions.",
                    "Revisar avisos e objetos somente no alvo antes de limpeza ou drop.")
            }
        };

        return acoes;
    }

    private static IReadOnlyList<ItemCriticoAlvo> MontarTopCriticosAlvo(ResultadoDiffSchema resultado, IdiomaSaida idioma)
    {
        var itens = new List<ItemCriticoAlvo>();

        itens.AddRange(resultado.ItensCriados.Select(i => CriarItemCritico("created", i, idioma)));
        itens.AddRange(resultado.ItensAlterados.Select(i => CriarItemCritico("changed", i, idioma)));
        itens.AddRange(resultado.Avisos.Select(i => CriarItemCritico("warning", i, idioma)));

        return itens
            .OrderByDescending(i => PesoSeveridade(i.Severidade))
            .ThenBy(i => i.Categoria, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Descricao, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static ItemCriticoAlvo CriarItemCritico(string origemItem, string texto, IdiomaSaida idioma)
    {
        string severidade = "LOW";
        string categoria = M(idioma, "Review", "Revisao");

        if (texto.Contains("Table missing", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("PK differs", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("Type differs", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("Nullability differs", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("FK missing", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("Tabela ausente", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("PK diferente", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("Tipo diferente", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("Nulidade diferente", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("FK ausente", StringComparison.OrdinalIgnoreCase))
        {
            severidade = "HIGH";
            categoria = M(idioma, "Consistency", "Consistencia");
        }
        else if (texto.Contains("Index missing", StringComparison.OrdinalIgnoreCase) ||
                 texto.Contains("Default differs", StringComparison.OrdinalIgnoreCase) ||
                 texto.Contains("Indice ausente", StringComparison.OrdinalIgnoreCase) ||
                 texto.Contains("Default diferente", StringComparison.OrdinalIgnoreCase))
        {
            severidade = "MEDIUM";
            categoria = M(idioma, "Optimization", "Otimizacao");
        }
        else if (origemItem == "warning")
        {
            severidade = "MEDIUM";
            categoria = M(idioma, "Manual review", "Revisao manual");
        }

        return new ItemCriticoAlvo
        {
            Severidade = severidade,
            Categoria = categoria,
            Descricao = texto
        };
    }

    private static int PesoSeveridade(string severidade)
    {
        return severidade switch
        {
            "HIGH" => 3,
            "MEDIUM" => 2,
            _ => 1
        };
    }

    private static IReadOnlyList<BlocoExecucaoSql> MontarBlocosExecucaoSql(IReadOnlyCollection<string> comandos, IdiomaSaida idioma)
    {
        int createTable = comandos.Count(c => c.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase));
        int alterTable = comandos.Count(c => c.Contains("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
                                             !c.Contains("ADD CONSTRAINT", StringComparison.OrdinalIgnoreCase));
        int pk = comandos.Count(c => c.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase));
        int fk = comandos.Count(c => c.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase));
        int index = comandos.Count(c => c.Contains("CREATE INDEX", StringComparison.OrdinalIgnoreCase) ||
                                        c.Contains("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase));

        return
        [
            new BlocoExecucaoSql
            {
                Ordem = 1,
                Titulo = M(idioma, "Base objects and columns", "Objetos base e colunas"),
                Quantidade = createTable + alterTable
            },
            new BlocoExecucaoSql
            {
                Ordem = 2,
                Titulo = M(idioma, "Primary keys and identity constraints", "Chaves primarias e restricoes de identidade"),
                Quantidade = pk
            },
            new BlocoExecucaoSql
            {
                Ordem = 3,
                Titulo = M(idioma, "Foreign keys and referential constraints", "Chaves estrangeiras e restricoes referenciais"),
                Quantidade = fk
            },
            new BlocoExecucaoSql
            {
                Ordem = 4,
                Titulo = M(idioma, "Indexes and performance objects", "Indices e objetos de performance"),
                Quantidade = index
            }
        ];
    }

    private static IReadOnlyList<string> MontarChecklistPosAplicacao(IdiomaSaida idioma)
    {
        return
        [
            M(idioma, "Run full backup and record execution window metadata.", "Executar backup completo e registrar metadados da janela de execucao."),
            M(idioma, "Validate object count parity between source and target snapshots.", "Validar paridade da contagem de objetos entre snapshots de origem e alvo."),
            M(idioma, "Execute integrity checks for PK/FK constraints and orphan rows.", "Executar validacoes de integridade para PK/FK e linhas orfas."),
            M(idioma, "Recompile procedures/views and check invalid dependencies.", "Recompilar procedures/views e checar dependencias invalidas."),
            M(idioma, "Review index selectivity and critical query plans.", "Revisar seletividade de indices e planos das consultas criticas."),
            M(idioma, "Monitor lock, IO and latency metrics in first production cycle.", "Monitorar metricas de lock, IO e latencia no primeiro ciclo em producao.")
        ];
    }

    private static string SecaoAcoesAlvoHtml(IReadOnlyList<AcaoAlvo> acoes, IdiomaSaida idioma)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine($"<h2>{Html(M(idioma, "Target Action Matrix (DBA)", "Matriz de Ação no Alvo (DBA)"))}</h2>");
        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine($"<th class=\"nowrap\">{Html(M(idioma, "Priority", "Prioridade"))}</th>");
        sb.AppendLine($"<th>{Html(M(idioma, "Focus", "Foco"))}</th>");
        sb.AppendLine($"<th class=\"nowrap\">{Html(M(idioma, "Count", "Qtde"))}</th>");
        sb.AppendLine($"<th>{Html(M(idioma, "Recommended action", "Ação recomendada"))}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var acao in acoes)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td class=\"nowrap\"><span class=\"chip {(acao.Prioridade == "P1" ? "warn" : "")}\">{Html(acao.Prioridade)}</span></td>");
            sb.AppendLine($"<td>{Html(acao.Tema)}</td>");
            sb.AppendLine($"<td class=\"nowrap\">{acao.Quantidade}</td>");
            sb.AppendLine($"<td>{Html(acao.Recomendacao)}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></div>");
        return sb.ToString();
    }

    private sealed class AcaoAlvo
    {
        public string Prioridade { get; init; } = "P3";
        public string Tema { get; init; } = string.Empty;
        public int Quantidade { get; init; }
        public string Recomendacao { get; init; } = string.Empty;
    }

    private sealed class ItemCriticoAlvo
    {
        public string Severidade { get; init; } = "LOW";
        public string Categoria { get; init; } = string.Empty;
        public string Descricao { get; init; } = string.Empty;
    }

    private sealed class BlocoExecucaoSql
    {
        public int Ordem { get; init; }
        public string Titulo { get; init; } = string.Empty;
        public int Quantidade { get; init; }
    }

    private sealed class AvisoDetalhado
    {
        public string Tipo { get; init; } = string.Empty;
        public string Severidade { get; init; } = "LOW";
        public string Escopo { get; init; } = string.Empty;
        public string Tabela { get; init; } = string.Empty;
        public string ObjetoA { get; init; } = string.Empty;
        public string ObjetoB { get; init; } = string.Empty;
        public string Objetos { get; init; } = string.Empty;
        public string Definicao { get; init; } = string.Empty;
        public string Acao { get; init; } = string.Empty;
        public bool Estruturado { get; init; }
        public string Mensagem { get; init; } = string.Empty;
    }

    private static string Html(string valor)
    {
        return System.Net.WebUtility.HtmlEncode(valor);
    }
}
