using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class RegraChavesEstrangeirasDdl : IRegraAnaliseDdl
{
    public void Avaliar(ContextoAnaliseDdl contexto)
    {
        foreach (var tabela in contexto.TabelasVisiveis.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarFks(tabela, contexto);
            ValidarDuplicidadeFks(tabela, contexto);
        }
    }

    private static void ValidarFks(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        var colunasLocais = tabela.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var fk in tabela.ChavesEstrangeiras)
        {
            string escopo = $"{tabela.Nome}.{fk.Nome}";

            if (!ValidarEstruturaFk(fk, escopo, contexto))
                continue;

            ValidarColunasLocaisFk(fk, colunasLocais, escopo, contexto);

            if (contexto.TabelasIgnoradas.Contains(fk.TabelaReferencia))
                continue;

            ValidarReferenciaFk(fk, escopo, contexto);
            ValidarIndiceCoberturaFk(tabela, fk, escopo, contexto);
        }
    }

    private static bool ValidarEstruturaFk(
        ChaveEstrangeiraSchema fk,
        string escopo,
        ContextoAnaliseDdl contexto)
    {
        if (fk.Colunas.Count == 0 || fk.ColunasReferencia.Count == 0)
        {
            contexto.AdicionarAchado(
                "critical",
                "FK_SEM_COLUNAS",
                escopo,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"FK {fk.Nome} has empty local/reference columns.",
                    $"FK {fk.Nome} possui colunas locais/referência vazias."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Recreate FK with explicit and ordered column list.",
                    "Recrie a FK com lista de colunas explícita e ordenada."));
            return false;
        }

        if (fk.Colunas.Count != fk.ColunasReferencia.Count)
        {
            contexto.AdicionarAchado(
                "critical",
                "FK_CARDINALIDADE_INVALIDA",
                escopo,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"FK {fk.Nome} has different local/reference column counts.",
                    $"FK {fk.Nome} possui cardinalidade diferente entre colunas locais e de referência."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Recreate FK preserving matching column cardinality.",
                    "Recrie a FK preservando cardinalidade equivalente."));
        }

        return true;
    }

    private static void ValidarColunasLocaisFk(
        ChaveEstrangeiraSchema fk,
        IReadOnlyDictionary<string, ColunaSchema> colunasLocais,
        string escopo,
        ContextoAnaliseDdl contexto)
    {
        foreach (var colunaFk in fk.Colunas)
        {
            if (colunasLocais.ContainsKey(colunaFk))
                continue;

            contexto.AdicionarAchado(
                "critical",
                "FK_COLUNA_LOCAL_INEXISTENTE",
                escopo,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"FK {fk.Nome} references missing local column {colunaFk}.",
                    $"FK {fk.Nome} referencia coluna local inexistente {colunaFk}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Validate relation fields and rebuild FK.",
                    "Valide os campos da relação e recrie a FK."));
        }
    }

    private static void ValidarReferenciaFk(
        ChaveEstrangeiraSchema fk,
        string escopo,
        ContextoAnaliseDdl contexto)
    {
        if (!contexto.MapaTabelasVisiveis.TryGetValue(fk.TabelaReferencia, out var tabelaReferencia))
        {
            contexto.AdicionarAchado(
                "critical",
                "FK_TABELA_REFERENCIA_INEXISTENTE",
                escopo,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"FK {fk.Nome} points to missing table {fk.TabelaReferencia}.",
                    $"FK {fk.Nome} aponta para tabela inexistente {fk.TabelaReferencia}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Validate dependency order and metadata integrity for referenced table.",
                    "Valide ordem de dependência e integridade de metadados da tabela referenciada."));
            return;
        }

        var colunasReferencia = tabelaReferencia.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        foreach (var colunaRef in fk.ColunasReferencia)
        {
            if (colunasReferencia.ContainsKey(colunaRef))
                continue;

            contexto.AdicionarAchado(
                "critical",
                "FK_COLUNA_REFERENCIA_INEXISTENTE",
                escopo,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"FK {fk.Nome} points to missing referenced column {colunaRef}.",
                    $"FK {fk.Nome} aponta para coluna referenciada inexistente {colunaRef}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Recreate FK after validating referenced key definition.",
                    "Recrie a FK após validar a definição da chave referenciada."));
        }
    }

    private static void ValidarIndiceCoberturaFk(
        TabelaSchema tabela,
        ChaveEstrangeiraSchema fk,
        string escopo,
        ContextoAnaliseDdl contexto)
    {
        if (!string.IsNullOrWhiteSpace(fk.IndiceSuporteNome))
            return;

        bool possuiIndiceCobertura = tabela.Indices.Any(indice => CobrePrefixo(indice.Colunas, fk.Colunas));
        if (possuiIndiceCobertura)
            return;

        contexto.AdicionarAchado(
            "medium",
            "FK_SEM_INDICE_COBERTURA",
            escopo,
            TextoLocalizado.Obter(
                contexto.Idioma,
                $"FK {fk.Nome} has no local covering index. Child table: {tabela.Nome} ({FormatarListaColunas(fk.Colunas)}). Parent table: {fk.TabelaReferencia} ({FormatarListaColunas(fk.ColunasReferencia)}).",
                $"FK {fk.Nome} não possui índice local de cobertura. Tabela filha: {tabela.Nome} ({FormatarListaColunas(fk.Colunas)}). Tabela pai: {fk.TabelaReferencia} ({FormatarListaColunas(fk.ColunasReferencia)})."),
            TextoLocalizado.Obter(
                contexto.Idioma,
                $"Create an index on child table {tabela.Nome} using FK columns ({FormatarListaColunas(fk.Colunas)}), preserving FK column order.",
                $"Crie um índice na tabela filha {tabela.Nome} usando as colunas da FK ({FormatarListaColunas(fk.Colunas)}), preservando a ordem da FK."));
    }

    private static void ValidarDuplicidadeFks(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        var gruposDuplicados = tabela.ChavesEstrangeiras
            .GroupBy(AssinaturaFk, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var grupo in gruposDuplicados)
        {
            string nomes = string.Join(", ", grupo.Select(f => f.Nome).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            contexto.AdicionarAchado(
                "low",
                "FK_DUPLICADA",
                tabela.Nome,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"Duplicated FK signature in {tabela.Nome}: {nomes}.",
                    $"Assinatura de FK duplicada em {tabela.Nome}: {nomes}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Consolidate equivalent foreign keys and keep only one validated constraint.",
                    "Consolide FKs equivalentes e mantenha apenas uma restrição validada."));
        }
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

    private static string FormatarListaColunas(IReadOnlyList<string> colunas)
    {
        if (colunas.Count == 0)
            return "-";

        return string.Join(", ", colunas);
    }
}
