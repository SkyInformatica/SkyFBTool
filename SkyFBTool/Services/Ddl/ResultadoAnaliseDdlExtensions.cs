namespace SkyFBTool.Services.Ddl;

internal static class ResultadoAnaliseDdlExtensions
{
    public static void AdicionarAchado(
        this ResultadoAnaliseDdl resultado,
        string severidade,
        string codigo,
        string escopo,
        string descricao,
        string recomendacao,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        string severidadeFinal = ConfiguracaoSeveridadeDdl.AplicarOverride(codigo, severidade, severidadesOverride);
        int scoreRisco = PontuacaoRiscoDdl.CalcularScoreRisco(severidadeFinal, codigo);

        resultado.Achados.Add(new AchadoAnaliseDdl
        {
            Severidade = severidadeFinal,
            ScoreRisco = scoreRisco,
            Prioridade = PontuacaoRiscoDdl.CalcularPrioridade(scoreRisco),
            Codigo = codigo,
            Escopo = escopo,
            Descricao = descricao,
            Recomendacao = recomendacao
        });
    }

    public static void AtualizarResumo(this ResultadoAnaliseDdl resultado)
    {
        resultado.Achados = resultado.Achados
            .OrderByDescending(a => a.ScoreRisco)
            .ThenByDescending(a => PontuacaoRiscoDdl.PesoSeveridade(a.Severidade))
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

    public static string NomeTabelaDoEscopo(string escopo)
    {
        if (string.IsNullOrWhiteSpace(escopo))
            return "?";

        int idx = escopo.IndexOf('.');
        return idx < 0 ? escopo : escopo[..idx];
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
}
