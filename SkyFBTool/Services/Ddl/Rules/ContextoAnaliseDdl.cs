using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class ContextoAnaliseDdl
{
    public ContextoAnaliseDdl(
        SnapshotSchema snapshot,
        ResultadoAnaliseDdl resultado,
        IdiomaSaida idioma,
        IReadOnlyList<TabelaSchema> tabelasVisiveis,
        IReadOnlyDictionary<string, TabelaSchema> mapaTabelasVisiveis,
        IReadOnlySet<string> tabelasIgnoradas,
        IReadOnlyDictionary<string, string>? severidadesOverride)
    {
        Snapshot = snapshot;
        Resultado = resultado;
        Idioma = idioma;
        TabelasVisiveis = tabelasVisiveis;
        MapaTabelasVisiveis = mapaTabelasVisiveis;
        TabelasIgnoradas = tabelasIgnoradas;
        SeveridadesOverride = severidadesOverride;
    }

    public SnapshotSchema Snapshot { get; }
    public ResultadoAnaliseDdl Resultado { get; }
    public IdiomaSaida Idioma { get; }
    public IReadOnlyList<TabelaSchema> TabelasVisiveis { get; }
    public IReadOnlyDictionary<string, TabelaSchema> MapaTabelasVisiveis { get; }
    public IReadOnlySet<string> TabelasIgnoradas { get; }
    public IReadOnlyDictionary<string, string>? SeveridadesOverride { get; }

    public void AdicionarAchado(
        string severidade,
        string codigo,
        string escopo,
        string descricao,
        string recomendacao)
    {
        Resultado.AdicionarAchado(severidade, codigo, escopo, descricao, recomendacao, SeveridadesOverride);
    }
}
