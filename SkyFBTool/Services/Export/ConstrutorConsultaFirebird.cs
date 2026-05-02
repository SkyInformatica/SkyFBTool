using System.Text;
using System.Text.RegularExpressions;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Export;

public static class ConstrutorConsultaFirebird
{
    private static readonly Regex RegexTabelaSimples =
        new(@"^[A-Za-z_][A-Za-z0-9_$]*$", RegexOptions.Compiled);

    private static readonly Regex RegexTabelaComAspas =
        new("^\"(?:[^\"]|\"\")+\"$", RegexOptions.Compiled);

    public static string MontarSelect(OpcoesExportacao opcoes)
    {
        if (!string.IsNullOrWhiteSpace(opcoes.ConsultaSqlCompleta))
            return ValidarSelectCompleto(opcoes.ConsultaSqlCompleta);

        var nomeTabela = ValidarNomeTabela(opcoes.Tabela);
        var where = NormalizarEValidarWhere(opcoes.CondicaoWhere);

        var sb = new StringBuilder();
        sb.Append("SELECT * FROM ").Append(nomeTabela);

        if (!string.IsNullOrWhiteSpace(where))
            sb.Append(" WHERE ").Append(where);

        return sb.ToString();
    }

    public static string MontarSelectComColunas(OpcoesExportacao opcoes, IReadOnlyList<string> colunas)
    {
        if (colunas is null || colunas.Count == 0)
            throw new ArgumentException("Nenhuma coluna vÃ¡lida foi informada para exportaÃ§Ã£o.");

        if (!string.IsNullOrWhiteSpace(opcoes.ConsultaSqlCompleta))
            return ValidarSelectCompleto(opcoes.ConsultaSqlCompleta);

        var nomeTabela = ValidarNomeTabela(opcoes.Tabela);
        var where = NormalizarEValidarWhere(opcoes.CondicaoWhere);

        var sb = new StringBuilder();
        sb.Append("SELECT ")
            .Append(string.Join(", ", colunas.Select(QuoteIdentifier)))
            .Append(" FROM ")
            .Append(nomeTabela);

        if (!string.IsNullOrWhiteSpace(where))
            sb.Append(" WHERE ").Append(where);

        return sb.ToString();
    }

    private static string ValidarSelectCompleto(string sql)
    {
        string texto = sql.Trim();
        if (texto.EndsWith(";"))
            texto = texto[..^1].TrimEnd();

        if (!texto.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Consulta SQL invÃ¡lida: o arquivo deve conter um SELECT.");

        return texto;
    }

    private static string ValidarNomeTabela(string? nomeTabela)
    {
        if (string.IsNullOrWhiteSpace(nomeTabela))
            throw new ArgumentException("Tabela nÃ£o informada (--table).");

        string nome = nomeTabela.Trim();

        if (ContemTokenPerigoso(nome))
            throw new ArgumentException("Nome de tabela invÃ¡lido: contÃ©m tokens SQL nÃ£o permitidos.");

        if (!RegexTabelaSimples.IsMatch(nome) && !RegexTabelaComAspas.IsMatch(nome))
            throw new ArgumentException("Nome de tabela invÃ¡lido: use identificador simples ou entre aspas.");

        return nome;
    }

    private static string? NormalizarEValidarWhere(string? condicaoWhere)
    {
        if (string.IsNullOrWhiteSpace(condicaoWhere))
            return null;

        string where = condicaoWhere.Trim();
        where = RemoverPrefixoWhere(where);

        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException("CondiÃ§Ã£o WHERE invÃ¡lida: vazia.");

        if (ContemTokenPerigoso(where))
            throw new ArgumentException("CondiÃ§Ã£o WHERE invÃ¡lida: contÃ©m tokens SQL nÃ£o permitidos.");

        return where;
    }

    private static string RemoverPrefixoWhere(string where)
    {
        if (!where.StartsWith("WHERE", StringComparison.OrdinalIgnoreCase))
            return where;

        if (where.Length == 5)
            return string.Empty;

        if (!char.IsWhiteSpace(where[5]))
            return where;

        return where[5..].TrimStart();
    }

    private static bool ContemTokenPerigoso(string valor)
    {
        return valor.Contains(';')
               || valor.Contains("--", StringComparison.Ordinal)
               || valor.Contains("/*", StringComparison.Ordinal)
               || valor.Contains("*/", StringComparison.Ordinal);
    }

    private static string QuoteIdentifier(string nomeColuna)
    {
        string nome = (nomeColuna ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome de coluna invÃ¡lido: vazio.");

        return $"\"{nome.Replace("\"", "\"\"")}\"";
    }
}
