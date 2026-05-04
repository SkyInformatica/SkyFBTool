using System.Text;

namespace SkyFBTool.Services.Ddl;

public static class GeradorDdlSql
{
    public static string Gerar(SnapshotSchema snapshot)
    {
        var sb = new StringBuilder();

        var auditoria = ValidadorCompatibilidadeCamposFirebird.Auditar(snapshot, snapshot.VersaoMajor);
        sb.AppendLine($"SET NAMES {ObterCharsetCabecalho(snapshot)};");
        sb.AppendLine();

        sb.AppendLine($"-- Firebird field compatibility audit: checked {auditoria.TotalItens} item(s), issues {auditoria.TotalProblemas}.");
        foreach (var item in auditoria.Itens.Where(i => i.Status != "ok"))
        {
            string limite = item.Limite.HasValue ? $" limit={item.Limite.Value}" : string.Empty;
            string valor = item.Valor.HasValue ? $" value={item.Valor.Value}" : string.Empty;
            string versao = item.VersaoMinimaMajor.HasValue ? $" requires>={item.VersaoMinimaMajor.Value}" : string.Empty;
            sb.AppendLine($"-- [{item.Status.ToUpperInvariant()}] {item.Codigo} | {item.Escopo} | {item.TipoSql}{versao}{limite}{valor}");
        }
        sb.AppendLine();

        sb.AppendLine("SET SQL DIALECT 3;");
        sb.AppendLine();

        AdicionarSecao(sb, snapshot.Dominios
            .Where(d => !EhDominioImplicito(d.Nome))
            .OrderBy(d => d.Nome, StringComparer.OrdinalIgnoreCase)
            .Select(dominio => GerarCreateDomain(dominio, snapshot.CharsetBanco)));
        AdicionarSecao(sb, snapshot.Sequencias.OrderBy(s => s.Nome, StringComparer.OrdinalIgnoreCase).Select(GerarCreateSequence));
        AdicionarSecao(sb, snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase).Select(tabela => GerarCreateTable(tabela, snapshot.CharsetBanco)));
        AdicionarSecao(sb, snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase).SelectMany(GerarDefinicoesTabela));
        AdicionarSecao(sb, snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase).SelectMany(t => t.Indices.OrderBy(i => i.Nome, StringComparer.OrdinalIgnoreCase).Select(indice => GerarCreateIndex(t, indice))));
        AdicionarSecao(sb, snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase).SelectMany(t => t.ChavesUnicas.OrderBy(u => u.Nome, StringComparer.OrdinalIgnoreCase).Select(unica => GerarAddUnique(t, unica))));
        AdicionarSecao(sb, snapshot.FuncoesExternas.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase).Select(f => f.SourceSql));
        AdicionarSecao(sb, snapshot.Views.OrderBy(v => v.Nome, StringComparer.OrdinalIgnoreCase).Select(GerarCreateView));

        if (snapshot.Funcoes.Count > 0)
        {
            sb.AppendLine("SET TERM ^;");
            sb.AppendLine();

            foreach (var funcao in snapshot.Funcoes.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(GerarBlocoPsql(funcao.SourceSql));
                sb.AppendLine();
            }

            sb.AppendLine("SET TERM ;^");
            sb.AppendLine();
        }

        if (snapshot.Procedimentos.Count > 0 || snapshot.Gatilhos.Count > 0)
        {
            sb.AppendLine("SET TERM ^;");
            sb.AppendLine();

            foreach (var procedimento in snapshot.Procedimentos.OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(GerarCreateProcedure(procedimento));
                sb.AppendLine();
            }

            foreach (var gatilho in snapshot.Gatilhos.OrderBy(g => g.Nome, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(GerarCreateTrigger(gatilho));
                sb.AppendLine();
            }

            sb.AppendLine("SET TERM ;^");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string ObterCharsetCabecalho(SnapshotSchema snapshot)
    {
        string? charset = snapshot.CharsetBanco?.Trim();
        return string.IsNullOrWhiteSpace(charset) ? "UTF8" : charset;
    }

    private static IEnumerable<string> GerarDefinicoesTabela(TabelaSchema tabela)
    {
        if (tabela.ChavePrimaria is not null)
            yield return GerarAddPk(tabela);

        foreach (var check in tabela.RestricoesCheck.OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase))
            yield return GerarAddCheck(tabela, check);

        foreach (var fk in tabela.ChavesEstrangeiras.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase))
            yield return GerarAddFk(tabela, fk);
    }

    private static void AdicionarSecao(StringBuilder sb, IEnumerable<string> comandos)
    {
        var lista = comandos.Where(comando => !string.IsNullOrWhiteSpace(comando)).ToList();
        if (lista.Count == 0)
            return;

        sb.AppendLine(string.Join(Environment.NewLine, lista));
        sb.AppendLine();
    }

    public static string GerarCreateDomain(DominioSchema dominio, string? charsetPadrao = null)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE DOMAIN {Q(dominio.Nome)} AS {dominio.TipoSql}");

        AppendCharset(sb, dominio.CharsetNome, charsetPadrao);

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

    public static string GerarCreateView(ViewSchema view)
    {
        return $"CREATE VIEW {Q(view.Nome)} AS {view.SelectSql.Trim()};";
    }

    public static string GerarCreateProcedure(ProcedimentoSchema procedimento)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE OR ALTER PROCEDURE {Q(procedimento.Nome)}");

        if (procedimento.ParametrosEntrada.Count > 0)
        {
            sb.AppendLine("(");
            sb.AppendLine(string.Join("," + Environment.NewLine, procedimento.ParametrosEntrada.Select(FormatarParametro)));
            sb.AppendLine(")");
        }

        if (procedimento.ParametrosSaida.Count > 0)
        {
            sb.AppendLine("RETURNS (");
            sb.AppendLine(string.Join("," + Environment.NewLine, procedimento.ParametrosSaida.Select(FormatarParametro)));
            sb.AppendLine(")");
        }

        sb.AppendLine("AS");
        sb.AppendLine(NormalizarBlocoFonteProcedimento(procedimento.SourceSql));
        sb.Append(" ^");
        return sb.ToString();
    }

    public static string GerarBlocoPsql(string fonte)
    {
        return fonte.Trim().TrimEnd(';') + " ^";
    }

    public static string GerarCreateTrigger(GatilhoSchema gatilho)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE OR ALTER TRIGGER {Q(gatilho.Nome)}");

        if (!string.IsNullOrWhiteSpace(gatilho.RelacaoNome))
            sb.Append($" FOR {Q(gatilho.RelacaoNome)}");

        sb.Append(' ');
        sb.Append(gatilho.Ativo ? "ACTIVE" : "INACTIVE");
        sb.Append(' ');
        sb.Append(FormatarCabecalhoTrigger(gatilho.TipoTrigger));

        if (gatilho.RelacaoNome is not null)
            sb.Append($" POSITION {gatilho.Sequencia}");

        sb.AppendLine();
        sb.AppendLine("AS");
        sb.AppendLine(NormalizarBlocoFonteTrigger(gatilho.SourceSql));
        sb.Append(" ^");
        return sb.ToString();
    }

    private static string NormalizarBlocoFonteProcedimento(string fonte)
    {
        string corpo = fonte.Trim().TrimEnd(';');
        if (corpo.StartsWith("AS", StringComparison.OrdinalIgnoreCase))
        {
            corpo = corpo[2..].TrimStart();
        }

        return corpo;
    }

    private static string NormalizarBlocoFonteTrigger(string fonte)
    {
        string corpo = fonte.Trim().TrimEnd(';');
        if (corpo.StartsWith("AS", StringComparison.OrdinalIgnoreCase))
            return corpo[2..].TrimStart();

        return corpo;
    }

    private static string FormatarCabecalhoTrigger(int tipoTrigger)
    {
        return tipoTrigger switch
        {
            1 => "BEFORE INSERT",
            2 => "AFTER INSERT",
            3 => "BEFORE UPDATE",
            4 => "AFTER UPDATE",
            5 => "BEFORE DELETE",
            6 => "AFTER DELETE",
            17 => "BEFORE INSERT OR UPDATE",
            18 => "AFTER INSERT OR UPDATE",
            25 => "BEFORE INSERT OR DELETE",
            26 => "AFTER INSERT OR DELETE",
            27 => "BEFORE UPDATE OR DELETE",
            28 => "AFTER UPDATE OR DELETE",
            113 => "BEFORE INSERT OR UPDATE OR DELETE",
            114 => "AFTER INSERT OR UPDATE OR DELETE",
            8192 => "ON CONNECT",
            8193 => "ON DISCONNECT",
            8194 => "ON TRANSACTION START",
            8195 => "ON TRANSACTION COMMIT",
            8196 => "ON TRANSACTION ROLLBACK",
            _ => $"/* UNKNOWN TRIGGER TYPE {tipoTrigger} */"
        };
    }

    private static string FormatarParametro(ParametroProcedimentoSchema parametro)
    {
        var sb = new StringBuilder();
        sb.Append(Q(parametro.Nome));
        sb.Append(' ');
        sb.Append(parametro.TipoSql);

        if (!parametro.AceitaNulo)
            sb.Append(" NOT NULL");

        if (!string.IsNullOrWhiteSpace(parametro.DefaultSql))
            sb.Append($" {parametro.DefaultSql}");

        return sb.ToString();
    }

    private static bool EhDominioImplicito(string nomeDominio)
    {
        return nomeDominio.StartsWith("RDB$", StringComparison.OrdinalIgnoreCase);
    }

    public static string GerarCreateTable(TabelaSchema tabela, string? charsetPadrao = null)
    {
        var linhasColunas = tabela.Colunas.Select(coluna => GerarDefinicaoColuna(coluna, charsetPadrao)).ToList();
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

    public static string GerarDefinicaoColuna(ColunaSchema coluna, string? charsetPadrao = null)
    {
        if (!string.IsNullOrWhiteSpace(coluna.ComputedBySql))
            return $"{Q(coluna.Nome)} COMPUTED BY {coluna.ComputedBySql}";

        string sql = $"{Q(coluna.Nome)} {coluna.TipoSql}";

        sql = AppendCharset(sql, coluna.CharsetNome, charsetPadrao);

        if (!string.IsNullOrWhiteSpace(coluna.DefaultSql))
            sql += $" {coluna.DefaultSql}";

        if (!coluna.AceitaNulo)
            sql += " NOT NULL";

        return sql;
    }

    private static void AppendCharset(StringBuilder sb, string? charsetNome, string? charsetPadrao = null)
    {
        if (DeveOmitirCharset(charsetNome, charsetPadrao))
            return;

        sb.Append($" CHARACTER SET {charsetNome}");
    }

    private static string AppendCharset(string sql, string? charsetNome, string? charsetPadrao = null)
    {
        if (DeveOmitirCharset(charsetNome, charsetPadrao))
            return sql;

        return $"{sql} CHARACTER SET {charsetNome}";
    }

    private static bool DeveOmitirCharset(string? charsetNome, string? charsetPadrao)
    {
        if (string.IsNullOrWhiteSpace(charsetNome))
            return true;

        if (string.IsNullOrWhiteSpace(charsetPadrao))
            return false;

        return string.Equals(
            charsetNome.Trim(),
            charsetPadrao.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ListaColunas(IEnumerable<string> colunas) => string.Join(", ", colunas.Select(Q));

    public static string Q(string identificador) =>
        $"\"{identificador.Replace("\"", "\"\"")}\"";
}
