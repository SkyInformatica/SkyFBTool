using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class RegraObjetosPsqlSomenteSuspendDdl : IRegraAnaliseDdl
{
    public void Avaliar(ContextoAnaliseDdl contexto)
    {
        foreach (var procedimento in contexto.Snapshot.Procedimentos.OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase))
        {
            if (procedimento.IgnorarValidacaoCorpoPsql)
                continue;

            ValidarObjeto(
                contexto,
                procedimento.Nome,
                procedimento.SourceSql,
                "PROCEDURE_SOMENTE_SUSPEND",
                "Procedure",
                "Procedure");
        }

        foreach (var funcao in contexto.Snapshot.Funcoes.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarObjeto(
                contexto,
                funcao.Nome,
                funcao.SourceSql,
                "FUNCTION_SOMENTE_SUSPEND",
                "Function",
                "Function");
        }

        foreach (var gatilho in contexto.Snapshot.Gatilhos.OrderBy(g => g.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarObjeto(
                contexto,
                gatilho.Nome,
                gatilho.SourceSql,
                "TRIGGER_SOMENTE_SUSPEND",
                "Trigger",
                "Trigger");
        }
    }

    private static void ValidarObjeto(
        ContextoAnaliseDdl contexto,
        string nome,
        string sourceSql,
        string codigo,
        string tipoIngles,
        string tipoPortugues)
    {
        if (!PsqlCorpoDdl.TryExtrairCorpoExecutavel(sourceSql, out string corpo))
            return;

        if (!string.Equals(PsqlCorpoDdl.NormalizarInstrucoes(corpo), "SUSPEND", StringComparison.Ordinal))
            return;

        contexto.AdicionarAchado(
            "high",
            codigo,
            nome,
            TextoLocalizado.Obter(
                contexto.Idioma,
                $"{tipoIngles} {nome} only executes SUSPEND and has no useful PSQL logic.",
                $"{tipoPortugues} {nome} executa apenas SUSPEND e não possui lógica PSQL útil."),
            TextoLocalizado.Obter(
                contexto.Idioma,
                "Validate whether the routine is intentionally empty; otherwise restore or remove obsolete metadata.",
                "Valide se a rotina está intencionalmente vazia; caso contrário, restaure ou remova o metadado obsoleto."));
    }
}
