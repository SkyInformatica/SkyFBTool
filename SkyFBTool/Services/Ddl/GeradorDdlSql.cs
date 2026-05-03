using System.Text;

namespace SkyFBTool.Services.Ddl;

public static class GeradorDdlSql
{
    public static string Gerar(SnapshotSchema snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SET SQL DIALECT 3;");
        sb.AppendLine();

        foreach (var dominio in snapshot.Dominios.OrderBy(d => d.Nome, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(GerarCreateDomain(dominio));
            sb.AppendLine();
        }

        foreach (var sequencia in snapshot.Sequencias.OrderBy(s => s.Nome, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(GerarCreateSequence(sequencia));
            sb.AppendLine();
        }

        foreach (var tabela in snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(GerarCreateTable(tabela));
            sb.AppendLine();
        }

        foreach (var tabela in snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            if (tabela.ChavePrimaria is not null)
                sb.AppendLine(GerarAddPk(tabela));

            foreach (var unica in tabela.ChavesUnicas.OrderBy(u => u.Nome, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(GerarAddUnique(tabela, unica));

            foreach (var check in tabela.RestricoesCheck.OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(GerarAddCheck(tabela, check));

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

    public static string GerarCreateDomain(DominioSchema dominio)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE DOMAIN {Q(dominio.Nome)} AS {dominio.TipoSql}");

        if (!string.IsNullOrWhiteSpace(dominio.DefaultSql))
            sb.Append($" {dominio.DefaultSql}");

        if (!dominio.AceitaNulo)
            sb.Append(" NOT NULL");

        if (!string.IsNullOrWhiteSpace(dominio.CheckSql))
            sb.Append($" {dominio.CheckSql}");

        sb.Append(';');
        return sb.ToString();
    }

    public static string GerarCreateSequence(SequenciaSchema sequencia)
    {
        return $"CREATE SEQUENCE {Q(sequencia.Nome)};";
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

    public static string GerarAddUnique(TabelaSchema tabela, ChaveUnicaSchema unica)
    {
        return $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(unica.Nome)} UNIQUE ({ListaColunas(unica.Colunas)});";
    }

    public static string GerarAddCheck(TabelaSchema tabela, RestricaoCheckSchema check)
    {
        string sql = check.CheckSql.Trim();
        if (!sql.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
            sql = $"CHECK ({sql})";

        return $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(check.Nome)} {sql};";
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
