namespace SkyFBTool.Services.Ddl;

internal static class PontuacaoRiscoDdl
{
    public static int PesoSeveridade(string severidade)
    {
        return severidade switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            _ => 1
        };
    }

    public static int CalcularScoreRisco(string severidade, string codigo)
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
            "FK_SEM_INDICE_COBERTURA" => 5,
            "INDICE_REDUNDANTE_PREFIXO" => -5,
            "INDICE_DUPLICADO" => -8,
            "FK_DUPLICADA" => -8,
            _ => 0
        };

        int score = baseScore + ajusteCodigo;
        return Math.Max(0, Math.Min(100, score));
    }

    public static string CalcularPrioridade(int scoreRisco)
    {
        if (scoreRisco >= 85) return "P0";
        if (scoreRisco >= 70) return "P1";
        if (scoreRisco >= 45) return "P2";
        return "P3";
    }
}
