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
        var nomeTabela = ValidarNomeTabela(opcoes.Tabela);
        var where = NormalizarEValidarWhere(opcoes.CondicaoWhere);

        var sb = new StringBuilder();
        sb.Append("SELECT * FROM ").Append(nomeTabela);

        if (!string.IsNullOrWhiteSpace(where))
            sb.Append(" WHERE ").Append(where);

        return sb.ToString();
    }

    private static string ValidarNomeTabela(string? nomeTabela)
    {
        if (string.IsNullOrWhiteSpace(nomeTabela))
            throw new ArgumentException("Tabela não informada (--table).");

        string nome = nomeTabela.Trim();

        if (ContemTokenPerigoso(nome))
            throw new ArgumentException("Nome de tabela inválido: contém tokens SQL não permitidos.");

        if (!RegexTabelaSimples.IsMatch(nome) && !RegexTabelaComAspas.IsMatch(nome))
            throw new ArgumentException("Nome de tabela inválido: use identificador simples ou entre aspas.");

        return nome;
    }

    private static string? NormalizarEValidarWhere(string? condicaoWhere)
    {
        if (string.IsNullOrWhiteSpace(condicaoWhere))
            return null;

        string where = condicaoWhere.Trim();
        if (where.StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase))
            where = where[6..].TrimStart();

        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException("Condição WHERE inválida: vazia.");

        if (ContemTokenPerigoso(where))
            throw new ArgumentException("Condição WHERE inválida: contém tokens SQL não permitidos.");

        return where;
    }

    private static bool ContemTokenPerigoso(string valor)
    {
        return valor.Contains(';')
               || valor.Contains("--", StringComparison.Ordinal)
               || valor.Contains("/*", StringComparison.Ordinal)
               || valor.Contains("*/", StringComparison.Ordinal);
    }
}
