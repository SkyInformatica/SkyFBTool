using System.Text;
using System.Text.Json;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl;

public static class ComparadorSchema
{
    public static async Task<(string ArquivoSql, string ArquivoJson, string ArquivoMarkdown)> CompararAsync(OpcoesDdlDiff opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Origem))
            throw new ArgumentException("Arquivo de origem nao informado (--source).");
        if (string.IsNullOrWhiteSpace(opcoes.Alvo))
            throw new ArgumentException("Arquivo de alvo nao informado (--target).");

        string origemJson = ResolverArquivoJsonSchema(opcoes.Origem);
        string alvoJson = ResolverArquivoJsonSchema(opcoes.Alvo);

        var origem = await LerSnapshotAsync(origemJson);
        var alvo = await LerSnapshotAsync(alvoJson);

        var resultado = GerarDiff(origem, alvo);
        var (arquivoSql, arquivoJson, arquivoMarkdown) = ResolverArquivosSaida(opcoes);

        Directory.CreateDirectory(Path.GetDirectoryName(arquivoSql)!);
        await File.WriteAllTextAsync(arquivoSql, MontarSql(resultado));
        await File.WriteAllTextAsync(arquivoJson, JsonSerializer.Serialize(resultado, JsonOptions));
        await File.WriteAllTextAsync(arquivoMarkdown, MontarMarkdown(resultado, origemJson, alvoJson));

        return (arquivoSql, arquivoJson, arquivoMarkdown);
    }

    public static ResultadoDiffSchema GerarDiff(SnapshotSchema origem, SnapshotSchema alvo)
    {
        var resultado = new ResultadoDiffSchema();

        var tabelasOrigem = origem.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);
        var tabelasAlvo = alvo.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var nomeTabela in tabelasOrigem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var tabelaOrigem = tabelasOrigem[nomeTabela];
            if (!tabelasAlvo.TryGetValue(nomeTabela, out var tabelaAlvo))
            {
                resultado.ItensCriados.Add($"Tabela ausente no alvo: {nomeTabela}");
                resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateTable(tabelaOrigem));

                if (tabelaOrigem.ChavePrimaria is not null)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddPk(tabelaOrigem));
                foreach (var fk in tabelaOrigem.ChavesEstrangeiras)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddFk(tabelaOrigem, fk));
                foreach (var indice in tabelaOrigem.Indices)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateIndex(tabelaOrigem, indice));

                continue;
            }

            CompararTabela(tabelaOrigem, tabelaAlvo, resultado);
        }

        foreach (var nomeTabelaAlvo in tabelasAlvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!tabelasOrigem.ContainsKey(nomeTabelaAlvo))
                resultado.ItensSomenteNoAlvo.Add($"Tabela existe apenas no alvo: {nomeTabelaAlvo}");
        }

        return resultado;
    }

    private static void CompararTabela(TabelaSchema origem, TabelaSchema alvo, ResultadoDiffSchema resultado)
    {
        var colunasOrigem = origem.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        var colunasAlvo = alvo.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var nomeColuna in colunasOrigem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var colunaOrigem = colunasOrigem[nomeColuna];
            if (!colunasAlvo.TryGetValue(nomeColuna, out var colunaAlvo))
            {
                resultado.ItensCriados.Add($"Coluna ausente no alvo: {origem.Nome}.{nomeColuna}");
                resultado.ComandosSql.Add(
                    $"ALTER TABLE {GeradorDdlSql.Q(origem.Nome)} ADD {GeradorDdlSql.GerarDefinicaoColuna(colunaOrigem)};");
                continue;
            }

            CompararColuna(origem.Nome, colunaOrigem, colunaAlvo, resultado);
        }

        foreach (var nomeColunaAlvo in colunasAlvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!colunasOrigem.ContainsKey(nomeColunaAlvo))
                resultado.ItensSomenteNoAlvo.Add($"Coluna existe apenas no alvo: {origem.Nome}.{nomeColunaAlvo}");
        }

        CompararPk(origem, alvo, resultado);
        CompararFks(origem, alvo, resultado);
        CompararIndices(origem, alvo, resultado);
    }

    private static void CompararColuna(string nomeTabela, ColunaSchema origem, ColunaSchema alvo, ResultadoDiffSchema resultado)
    {
        if (!string.Equals(origem.ComputedBySql, alvo.ComputedBySql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.Avisos.Add($"Coluna computada diferente: {nomeTabela}.{origem.Nome} (ajuste manual recomendado).");
            return;
        }

        if (!string.Equals(origem.TipoSql, alvo.TipoSql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.ItensAlterados.Add($"Tipo diferente: {nomeTabela}.{origem.Nome} ({alvo.TipoSql} -> {origem.TipoSql})");
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} TYPE {origem.TipoSql};");
        }

        if (origem.AceitaNulo != alvo.AceitaNulo)
        {
            resultado.ItensAlterados.Add($"Nulidade diferente: {nomeTabela}.{origem.Nome}");
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} {(origem.AceitaNulo ? "DROP" : "SET")} NOT NULL;");
        }

        if (!string.Equals(origem.DefaultSql, alvo.DefaultSql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.ItensAlterados.Add($"Default diferente: {nomeTabela}.{origem.Nome}");
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

    private static void CompararPk(TabelaSchema origem, TabelaSchema alvo, ResultadoDiffSchema resultado)
    {
        string assinaturaOrigem = AssinaturaPk(origem.ChavePrimaria);
        string assinaturaAlvo = AssinaturaPk(alvo.ChavePrimaria);
        if (assinaturaOrigem == assinaturaAlvo)
            return;

        resultado.ItensAlterados.Add($"PK diferente: {origem.Nome}");
        if (alvo.ChavePrimaria is not null)
        {
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(origem.Nome)} DROP CONSTRAINT {GeradorDdlSql.Q(alvo.ChavePrimaria.Nome)};");
        }

        if (origem.ChavePrimaria is not null)
            resultado.ComandosSql.Add(GeradorDdlSql.GerarAddPk(origem));
    }

    private static void CompararFks(TabelaSchema origem, TabelaSchema alvo, ResultadoDiffSchema resultado)
    {
        var fksOrigem = origem.ChavesEstrangeiras.ToDictionary(AssinaturaFk, StringComparer.OrdinalIgnoreCase);
        var fksAlvo = alvo.ChavesEstrangeiras.ToDictionary(AssinaturaFk, StringComparer.OrdinalIgnoreCase);

        foreach (var assinatura in fksOrigem.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (fksAlvo.ContainsKey(assinatura))
                continue;

            resultado.ItensCriados.Add($"FK ausente no alvo: {origem.Nome}.{fksOrigem[assinatura].Nome}");
            resultado.ComandosSql.Add(GeradorDdlSql.GerarAddFk(origem, fksOrigem[assinatura]));
        }

        foreach (var assinatura in fksAlvo.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (!fksOrigem.ContainsKey(assinatura))
                resultado.ItensSomenteNoAlvo.Add($"FK existe apenas no alvo: {origem.Nome}.{fksAlvo[assinatura].Nome}");
        }
    }

    private static void CompararIndices(TabelaSchema origem, TabelaSchema alvo, ResultadoDiffSchema resultado)
    {
        var idxOrigem = origem.Indices.ToDictionary(AssinaturaIndice, StringComparer.OrdinalIgnoreCase);
        var idxAlvo = alvo.Indices.ToDictionary(AssinaturaIndice, StringComparer.OrdinalIgnoreCase);

        foreach (var assinatura in idxOrigem.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (idxAlvo.ContainsKey(assinatura))
                continue;

            resultado.ItensCriados.Add($"Indice ausente no alvo: {origem.Nome}.{idxOrigem[assinatura].Nome}");
            resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateIndex(origem, idxOrigem[assinatura]));
        }

        foreach (var assinatura in idxAlvo.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (!idxOrigem.ContainsKey(assinatura))
                resultado.ItensSomenteNoAlvo.Add($"Indice existe apenas no alvo: {origem.Nome}.{idxAlvo[assinatura].Nome}");
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

    private static (string ArquivoSql, string ArquivoJson, string ArquivoMarkdown) ResolverArquivosSaida(OpcoesDdlDiff opcoes)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string padrao = $"schema_diff_{timestamp}";

        if (string.IsNullOrWhiteSpace(opcoes.Saida))
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), padrao);
            return ($"{basePath}.sql", $"{basePath}.json", $"{basePath}.md");
        }

        string saida = opcoes.Saida.Trim();
        if (Directory.Exists(saida) || saida.EndsWith(Path.DirectorySeparatorChar) || saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string basePath = Path.Combine(Path.GetFullPath(saida), padrao);
            return ($"{basePath}.sql", $"{basePath}.json", $"{basePath}.md");
        }

        string semExtensao = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(saida)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(saida));

        return ($"{semExtensao}.sql", $"{semExtensao}.json", $"{semExtensao}.md");
    }

    private static string MontarSql(ResultadoDiffSchema resultado)
    {
        if (resultado.ComandosSql.Count == 0)
            return "-- Nenhuma diferenca que exija SQL automatico." + Environment.NewLine;

        return string.Join(Environment.NewLine, resultado.ComandosSql) + Environment.NewLine;
    }

    private static string MontarMarkdown(ResultadoDiffSchema resultado, string origemJson, string alvoJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Relatorio DDL Diff");
        sb.AppendLine();
        sb.AppendLine($"- Origem: `{origemJson}`");
        sb.AppendLine($"- Alvo: `{alvoJson}`");
        sb.AppendLine();
        sb.AppendLine("## Resumo");
        sb.AppendLine();
        sb.AppendLine($"- Comandos SQL gerados: {resultado.ComandosSql.Count}");
        sb.AppendLine($"- Itens criados no alvo: {resultado.ItensCriados.Count}");
        sb.AppendLine($"- Itens alterados: {resultado.ItensAlterados.Count}");
        sb.AppendLine($"- Itens somente no alvo: {resultado.ItensSomenteNoAlvo.Count}");
        sb.AppendLine($"- Avisos: {resultado.Avisos.Count}");
        sb.AppendLine();

        AdicionarSecao(sb, "Itens criados", resultado.ItensCriados);
        AdicionarSecao(sb, "Itens alterados", resultado.ItensAlterados);
        AdicionarSecao(sb, "Itens somente no alvo", resultado.ItensSomenteNoAlvo);
        AdicionarSecao(sb, "Avisos", resultado.Avisos);

        return sb.ToString();
    }

    private static void AdicionarSecao(StringBuilder sb, string titulo, IReadOnlyCollection<string> itens)
    {
        sb.AppendLine($"## {titulo}");
        sb.AppendLine();
        if (itens.Count == 0)
        {
            sb.AppendLine("- Nenhum");
            sb.AppendLine();
            return;
        }

        foreach (var item in itens.OrderBy(i => i, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"- {item}");
        sb.AppendLine();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
