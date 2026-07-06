using System.Text;
using System.Text.RegularExpressions;

namespace SkyFBTool.Services.Ddl.Rules;

internal static class PsqlCorpoDdl
{
    private static readonly Regex RegexCorpoPsqlBegin = new(
        @"\bBEGIN\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RegexCorpoPsqlEnd = new(
        @"\bEND\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryExtrairCorpoExecutavel(string sourceSql, out string corpo)
    {
        corpo = string.Empty;
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

        corpo = semComentarios[(matchBegin.Index + matchBegin.Length)..indiceEnd];
        return true;
    }

    public static bool PossuiInstrucaoExecutavel(string sourceSql)
    {
        return TryExtrairCorpoExecutavel(sourceSql, out string corpo) &&
               corpo.Any(c => !char.IsWhiteSpace(c) && c != ';');
    }

    public static string NormalizarInstrucoes(string corpo)
    {
        var sb = new StringBuilder(corpo.Length);
        foreach (char c in corpo)
        {
            if (char.IsWhiteSpace(c) || c == ';')
                continue;

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
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
