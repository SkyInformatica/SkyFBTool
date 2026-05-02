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
        => MontarSelect(opcoes, IdiomaSaida.English);

    public static string MontarSelect(OpcoesExportacao opcoes, IdiomaSaida idioma)
    {
        if (!string.IsNullOrWhiteSpace(opcoes.ConsultaSqlCompleta))
            return ValidarSelectCompleto(opcoes.ConsultaSqlCompleta, idioma);

        var nomeTabela = ValidarNomeTabela(opcoes.Tabela, idioma);
        var where = NormalizarEValidarWhere(opcoes.CondicaoWhere, idioma);

        var sb = new StringBuilder();
        sb.Append("SELECT * FROM ").Append(nomeTabela);

        if (!string.IsNullOrWhiteSpace(where))
            sb.Append(" WHERE ").Append(where);

        return sb.ToString();
    }

    public static string MontarSelectComColunas(OpcoesExportacao opcoes, IReadOnlyList<string> colunas)
        => MontarSelectComColunas(opcoes, colunas, IdiomaSaida.English);

    public static string MontarSelectComColunas(OpcoesExportacao opcoes, IReadOnlyList<string> colunas, IdiomaSaida idioma)
    {
        if (colunas is null || colunas.Count == 0)
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "No valid columns were provided for export.",
                "Nenhuma coluna válida foi informada para exportação."));

        if (!string.IsNullOrWhiteSpace(opcoes.ConsultaSqlCompleta))
            return ValidarSelectCompleto(opcoes.ConsultaSqlCompleta, idioma);

        var nomeTabela = ValidarNomeTabela(opcoes.Tabela, idioma);
        var where = NormalizarEValidarWhere(opcoes.CondicaoWhere, idioma);

        var sb = new StringBuilder();
        sb.Append("SELECT ")
            .Append(string.Join(", ", colunas.Select(QuoteIdentifier)))
            .Append(" FROM ")
            .Append(nomeTabela);

        if (!string.IsNullOrWhiteSpace(where))
            sb.Append(" WHERE ").Append(where);

        return sb.ToString();
    }

    private static string ValidarSelectCompleto(string sql, IdiomaSaida idioma)
    {
        string texto = sql.Trim();
        if (texto.EndsWith(";"))
            texto = texto[..^1].TrimEnd();

        if (!texto.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Invalid SQL query: the file must contain a SELECT.",
                "Consulta SQL inválida: o arquivo deve conter um SELECT."));

        return texto;
    }

    private static string ValidarNomeTabela(string? nomeTabela, IdiomaSaida idioma)
    {
        if (string.IsNullOrWhiteSpace(nomeTabela))
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Table not provided (--table).",
                "Tabela não informada (--table)."));

        string nome = nomeTabela.Trim();

        if (ContemTokenPerigoso(nome))
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Invalid table name: contains forbidden SQL tokens.",
                "Nome de tabela inválido: contém tokens SQL não permitidos."));

        if (!RegexTabelaSimples.IsMatch(nome) && !RegexTabelaComAspas.IsMatch(nome))
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Invalid table name: use a simple identifier or a quoted identifier.",
                "Nome de tabela inválido: use identificador simples ou entre aspas."));

        return nome;
    }

    private static string? NormalizarEValidarWhere(string? condicaoWhere, IdiomaSaida idioma)
    {
        if (string.IsNullOrWhiteSpace(condicaoWhere))
            return null;

        string where = condicaoWhere.Trim();
        where = RemoverPrefixoWhere(where);

        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Invalid WHERE condition: empty.",
                "Condição WHERE inválida: vazia."));

        if (ContemTokenPerigoso(where))
            throw new ArgumentException(TextoLocalizado.Obter(idioma,
                "Invalid WHERE condition: contains forbidden SQL tokens.",
                "Condição WHERE inválida: contém tokens SQL não permitidos."));

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
            throw new ArgumentException("Invalid column name: empty.");

        return $"\"{nome.Replace("\"", "\"\"")}\"";
    }
}
