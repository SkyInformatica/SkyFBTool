using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class RegraObjetosPsqlSemCorpoDdl : IRegraAnaliseDdl
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
                "PROCEDURE_SEM_CORPO",
                "Procedure",
                "Procedure",
                "RDB$PROCEDURES.RDB$PROCEDURE_SOURCE");
        }

        foreach (var funcao in contexto.Snapshot.Funcoes.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarObjeto(
                contexto,
                funcao.Nome,
                funcao.SourceSql,
                "FUNCTION_SEM_CORPO",
                "Function",
                "Function",
                "RDB$FUNCTIONS.RDB$FUNCTION_SOURCE");
        }

        foreach (var gatilho in contexto.Snapshot.Gatilhos.OrderBy(g => g.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarObjeto(
                contexto,
                gatilho.Nome,
                gatilho.SourceSql,
                "TRIGGER_SEM_CORPO",
                "Trigger",
                "Trigger",
                "RDB$TRIGGERS.RDB$TRIGGER_SOURCE");
        }
    }

    private static void ValidarObjeto(
        ContextoAnaliseDdl contexto,
        string nome,
        string sourceSql,
        string codigo,
        string tipoIngles,
        string tipoPortugues,
        string fonteCatalogo)
    {
        if (PsqlCorpoDdl.PossuiInstrucaoExecutavel(sourceSql))
            return;

        contexto.AdicionarAchado(
            "critical",
            codigo,
            nome,
            TextoLocalizado.Obter(
                contexto.Idioma,
                $"{tipoIngles} {nome} has no valid PSQL body.",
                $"{tipoPortugues} {nome} não possui corpo PSQL válido."),
            TextoLocalizado.Obter(
                contexto.Idioma,
                $"Re-extract metadata and validate {fonteCatalogo} before generating or applying DDL.",
                $"Reextraia o metadado e valide {fonteCatalogo} antes de gerar ou aplicar o DDL."));
    }

}
