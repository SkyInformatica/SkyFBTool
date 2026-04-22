using System.Text;

namespace SkyFBTool.Services.Ddl;

public static class GeradorDdlSql
{
    public static string Gerar(SnapshotSchema snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SET SQL DIALECT 3;");
        sb.AppendLine();

        foreach (var tabela in snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(GerarCreateTable(tabela));
            sb.AppendLine();
        }

        foreach (var tabela in snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            if (tabela.ChavePrimaria is not null)
                sb.AppendLine(GerarAddPk(tabela));

            foreach (var fk in tabela.ChavesEstrangeiras.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(GerarAddFk(tabela, fk));
        }

        if (snapshot.Tabelas.Any(t => t.Indices.Count > 0))
            sb.AppendLine();

        foreach (var tabela in snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var indice in tabela.Indices.OrderBy(i => i.Nome, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(GerarCreateIndex(tabela, indice));
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public static string GerarCreateTable(TabelaSchema tabela)
    {
        var linhasColunas = tabela.Colunas.Select(GerarDefinicaoColuna).ToList();
        var conteudo = string.Join("," + Environment.NewLine, linhasColunas.Select(l => $"    {l}"));
        return $"CREATE TABLE {Q(tabela.Nome)} ({Environment.NewLine}{conteudo}{Environment.NewLine});";
    }

    public static string GerarAddPk(TabelaSchema tabela)
    {
        var pk = tabela.ChavePrimaria!;
        return $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(pk.Nome)} PRIMARY KEY ({ListaColunas(pk.Colunas)});";
    }

    public static string GerarAddFk(TabelaSchema tabela, ChaveEstrangeiraSchema fk)
    {
        string sql =
            $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(fk.Nome)} " +
            $"FOREIGN KEY ({ListaColunas(fk.Colunas)}) REFERENCES {Q(fk.TabelaReferencia)} ({ListaColunas(fk.ColunasReferencia)})";

        if (!string.Equals(fk.RegraUpdate, "RESTRICT", StringComparison.OrdinalIgnoreCase))
            sql += $" ON UPDATE {fk.RegraUpdate}";
        if (!string.Equals(fk.RegraDelete, "RESTRICT", StringComparison.OrdinalIgnoreCase))
            sql += $" ON DELETE {fk.RegraDelete}";

        return sql + ";";
    }

    public static string GerarCreateIndex(TabelaSchema tabela, IndiceSchema indice)
    {
        string prefixo = indice.Unico ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
        string desc = indice.Descendente ? " DESCENDING" : string.Empty;
        return $"{prefixo} {Q(indice.Nome)} ON {Q(tabela.Nome)}{desc} ({ListaColunas(indice.Colunas)});";
    }

    public static string GerarDefinicaoColuna(ColunaSchema coluna)
    {
        if (!string.IsNullOrWhiteSpace(coluna.ComputedBySql))
            return $"{Q(coluna.Nome)} COMPUTED BY {coluna.ComputedBySql}";

        string sql = $"{Q(coluna.Nome)} {coluna.TipoSql}";

        if (!string.IsNullOrWhiteSpace(coluna.DefaultSql))
            sql += $" {coluna.DefaultSql}";

        if (!coluna.AceitaNulo)
            sql += " NOT NULL";

        return sql;
    }

    private static string ListaColunas(IEnumerable<string> colunas) => string.Join(", ", colunas.Select(Q));

    public static string Q(string identificador) =>
        $"\"{identificador.Replace("\"", "\"\"")}\"";
}
