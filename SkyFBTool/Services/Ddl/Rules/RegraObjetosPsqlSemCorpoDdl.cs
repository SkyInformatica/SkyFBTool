using System.Text;
using System.Text.RegularExpressions;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class RegraObjetosPsqlSemCorpoDdl : IRegraAnaliseDdl
{
    private static readonly Regex RegexCorpoPsqlBegin = new(
        @"\bBEGIN\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RegexCorpoPsqlEnd = new(
        @"\bEND\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public void Avaliar(ContextoAnaliseDdl contexto)
    {
        foreach (var procedimento in contexto.Snapshot.Procedimentos.OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase))
        {
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
        if (PossuiCorpoPsql(sourceSql))
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

    private static bool PossuiCorpoPsql(string sourceSql)
    {
        if (string.IsNullOrWhiteSpace(sourceSql))
            return false;

        string semComentarios = RemoverComentariosSqlPreservandoStrings(sourceSql);
        var matchBegin = RegexCorpoPsqlBegin.Match(semComentarios);
        if (!matchBegin.Success)
            return false;

        int indiceEnd = RegexCorpoPsqlEnd.Matches(semComentarios)
            .Cast<Match>()
            .LastOrDefault()
            ?.Index ?? -1;
        if (indiceEnd <= matchBegin.Index + matchBegin.Length)
            return false;

        string corpo = semComentarios[(matchBegin.Index + matchBegin.Length)..indiceEnd];
        return corpo.Any(c => !char.IsWhiteSpace(c) && c != ';');
    }

    private static string RemoverComentariosSqlPreservandoStrings(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        bool dentroString = false;
        bool dentroComentarioLinha = false;
        bool dentroComentarioBloco = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];

            if (dentroComentarioLinha)
            {
                if (c is '\r' or '\n')
                {
                    dentroComentarioLinha = false;
                    sb.Append(c);
                }

                continue;
            }

            if (dentroComentarioBloco)
            {
                if (c == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                {
                    dentroComentarioBloco = false;
                    i++;
                }

                continue;
            }

            if (dentroString)
            {
                sb.Append(c);
                if (c == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    sb.Append(sql[i + 1]);
                    i++;
                    continue;
                }

                if (c == '\'')
                    dentroString = false;

                continue;
            }

            if (c == '\'')
            {
                dentroString = true;
                sb.Append(c);
                continue;
            }

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                dentroComentarioLinha = true;
                i++;
                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                dentroComentarioBloco = true;
                i++;
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
