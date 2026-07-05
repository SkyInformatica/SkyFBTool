using System.Text.RegularExpressions;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class RegraIndicesDdl : IRegraAnaliseDdl
{
    private static readonly Regex RegexOperadorExpressaoIndice = new(
        @"(?:\s[+\-*/]\s|\|\||<>|<=|>=|=|<|>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RegexPalavraChaveExpressaoIndice = new(
        @"\b(?:CASE|WHEN|THEN|ELSE|END|FROM|AS|IS|NULL|NOT|AND|OR)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RegexIdentificadorSimples = new(
        @"^[A-Z_][A-Z0-9_$]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public void Avaliar(ContextoAnaliseDdl contexto)
    {
        foreach (var tabela in contexto.TabelasVisiveis.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarIndices(tabela, contexto);
            ValidarDuplicidadeIndices(tabela, contexto);
            ValidarRedundanciaIndicesPrefixo(tabela, contexto);
        }
    }

    private static void ValidarIndices(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        var colunas = tabela.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var indice in tabela.Indices)
        {
            string escopo = $"{tabela.Nome}.{indice.Nome}";

            if (indice.Colunas.Count == 0)
            {
                contexto.AdicionarAchado(
                    "high",
                    "INDICE_SEM_COLUNAS",
                    escopo,
                    TextoLocalizado.Obter(
                        contexto.Idioma,
                        $"Index {indice.Nome} has no columns.",
                        $"Índice {indice.Nome} não possui colunas."),
                    TextoLocalizado.Obter(
                        contexto.Idioma,
                        "Recreate index with explicit column list.",
                        "Recrie o índice com lista explícita de colunas."));
                continue;
            }

            foreach (var colunaIndice in indice.Colunas)
            {
                if (colunas.ContainsKey(colunaIndice))
                    continue;

                if (EhExpressaoIndice(colunaIndice))
                    continue;

                contexto.AdicionarAchado(
                    "high",
                    "INDICE_COLUNA_INEXISTENTE",
                    escopo,
                    TextoLocalizado.Obter(
                        contexto.Idioma,
                        $"Index {indice.Nome} references missing column {colunaIndice}.",
                        $"Índice {indice.Nome} referencia coluna inexistente {colunaIndice}."),
                    TextoLocalizado.Obter(
                        contexto.Idioma,
                        "Recreate index and validate relation fields catalog.",
                        "Recrie o índice e valide o catálogo de campos da relação."));
            }
        }
    }

    private static bool EhExpressaoIndice(string colunaIndice)
    {
        string valor = colunaIndice.Trim();
        if (!RegexIdentificadorSimples.IsMatch(valor))
            return true;

        if (valor.Contains('(') || valor.Contains(')') || valor.Contains('\''))
            return true;

        if (RegexOperadorExpressaoIndice.IsMatch(valor))
            return true;

        return RegexPalavraChaveExpressaoIndice.IsMatch(valor);
    }

    private static void ValidarDuplicidadeIndices(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        var grupos = tabela.Indices
            .GroupBy(AssinaturaIndice, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var grupo in grupos)
        {
            string nomes = string.Join(", ", grupo.Select(i => i.Nome).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            string assinaturaExibicao = FormatarAssinaturaComparacaoIndice(grupo.First());
            contexto.AdicionarAchado(
                "low",
                "INDICE_DUPLICADO",
                tabela.Nome,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"Duplicated index signature in {tabela.Nome}: {nomes}. Signature: {assinaturaExibicao}.",
                    $"Assinatura de índice duplicada em {tabela.Nome}: {nomes}. Assinatura: {assinaturaExibicao}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Keep only one index per signature after workload validation.",
                    "Mantenha apenas um índice por assinatura após validar carga de trabalho."));
        }
    }

    private static void ValidarRedundanciaIndicesPrefixo(TabelaSchema tabela, ContextoAnaliseDdl contexto)
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

                contexto.AdicionarAchado(
                    "medium",
                    "INDICE_REDUNDANTE_PREFIXO",
                    tabela.Nome,
                    TextoLocalizado.Obter(
                        contexto.Idioma,
                        $"Index {indiceCurto.Nome} ({FormatarAssinaturaComparacaoIndice(indiceCurto)}) may be redundant because {indiceLongo.Nome} ({FormatarAssinaturaComparacaoIndice(indiceLongo)}) already covers its prefix ({FormatarListaColunas(indiceCurto.Colunas)}).",
                        $"Índice {indiceCurto.Nome} ({FormatarAssinaturaComparacaoIndice(indiceCurto)}) pode ser redundante porque {indiceLongo.Nome} ({FormatarAssinaturaComparacaoIndice(indiceLongo)}) já cobre seu prefixo ({FormatarListaColunas(indiceCurto.Colunas)})."),
                    TextoLocalizado.Obter(
                        contexto.Idioma,
                        "Validate query plans and keep only the index with better selectivity/coverage.",
                        "Valide planos de execução e mantenha apenas o índice com melhor seletividade/cobertura."));

                break;
            }
        }
    }

    private static string AssinaturaIndice(IndiceSchema indice)
    {
        return $"{(indice.Unico ? "U" : "N")}|{(indice.Descendente ? "D" : "A")}|{string.Join("|", indice.Colunas).ToUpperInvariant()}";
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
}
