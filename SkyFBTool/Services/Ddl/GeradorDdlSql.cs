using System.Text;
using System.Text.RegularExpressions;

namespace SkyFBTool.Services.Ddl;

public static class GeradorDdlSql
{
    private static readonly Regex RegexConstraintInteg = new("^INTEG_\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        var tabelasOrdenadas = snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase).ToList();

        AdicionarSecao(sb, tabelasOrdenadas.Where(t => t.ChavePrimaria is not null).Select(GerarAddPk));
        AdicionarSecao(sb, tabelasOrdenadas.SelectMany(t => t.ChavesUnicas.OrderBy(u => u.Nome, StringComparer.OrdinalIgnoreCase).Select(unica => GerarAddUnique(t, unica))));
        AdicionarSecao(sb, tabelasOrdenadas.SelectMany(t => t.RestricoesCheck.OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase).Select(check => GerarAddCheck(t, check))));
        AdicionarSecao(sb, tabelasOrdenadas.SelectMany(t => t.ChavesEstrangeiras.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase).Select(fk => GerarAddFk(t, fk))));
        AdicionarSecao(sb, tabelasOrdenadas.SelectMany(t => t.Indices.OrderBy(i => i.Nome, StringComparer.OrdinalIgnoreCase).Select(indice => GerarCreateIndex(t, indice))));
        AdicionarSecao(sb, snapshot.FuncoesExternas.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase).Select(f => f.SourceSql));
        AdicionarSecao(sb, snapshot.Views.OrderBy(v => v.Nome, StringComparer.OrdinalIgnoreCase).Select(GerarCreateView));
        AdicionarSecao(sb, snapshot.Excecoes.OrderBy(e => e.Nome, StringComparer.OrdinalIgnoreCase).Select(GerarCreateException));

        if (snapshot.Funcoes.Count > 0)
        {
            sb.AppendLine("SET TERM ^;");
            sb.AppendLine();

            foreach (var funcao in snapshot.Funcoes.OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(GerarBlocoPsql(funcao.SourceSql, "FUNCTION", funcao.Nome));
                sb.AppendLine();
            }

            sb.AppendLine("SET TERM ;^");
            sb.AppendLine();
        }

        if (snapshot.Procedimentos.Count > 0 || snapshot.Gatilhos.Count > 0)
        {
            AjustarDefaultsParametrosProcedimentoParaCompatibilidade(snapshot.Procedimentos);
            var procedimentosOrdenados = OrdenarProcedimentosPorDependencia(snapshot.Procedimentos);

            sb.AppendLine("SET TERM ^;");
            sb.AppendLine();

            foreach (var procedimento in procedimentosOrdenados.Where(PossuiFontePsql))
            {
                sb.AppendLine(GerarCreateProcedureStub(procedimento));
                sb.AppendLine();
            }

            foreach (var procedimento in procedimentosOrdenados)
            {
                sb.AppendLine(PossuiFontePsql(procedimento)
                    ? GerarCreateProcedure(procedimento)
                    : GerarComentarioPsqlSemFonte("PROCEDURE", procedimento.Nome));
                sb.AppendLine();
            }

            foreach (var gatilho in snapshot.Gatilhos.OrderBy(g => g.Nome, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(PossuiFontePsql(gatilho)
                    ? GerarCreateTrigger(gatilho)
                    : GerarComentarioPsqlSemFonte("TRIGGER", gatilho.Nome));
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

    public static string GerarCreateException(ExcecaoSchema excecao)
    {
        string mensagem = (excecao.Mensagem ?? string.Empty).Replace("'", "''");
        return $"CREATE EXCEPTION {Q(excecao.Nome)} '{mensagem}';";
    }

    public static string GerarCreateProcedure(ProcedimentoSchema procedimento)
    {
        if (!PossuiFontePsql(procedimento))
            return GerarComentarioPsqlSemFonte("PROCEDURE", procedimento.Nome);

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

    public static string GerarCreateProcedureStub(ProcedimentoSchema procedimento)
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
        sb.AppendLine("BEGIN");
        sb.AppendLine("END");
        sb.Append(" ^");
        return sb.ToString();
    }

    public static string GerarBlocoPsql(string fonte)
    {
        return GerarBlocoPsql(fonte, "PSQL object", null);
    }

    private static string GerarBlocoPsql(string fonte, string tipoObjeto, string? nomeObjeto)
    {
        if (string.IsNullOrWhiteSpace(fonte))
            return GerarComentarioPsqlSemFonte(tipoObjeto, nomeObjeto);

        return fonte.Trim().TrimEnd(';') + " ^";
    }

    public static string GerarCreateTrigger(GatilhoSchema gatilho)
    {
        if (!PossuiFontePsql(gatilho))
            return GerarComentarioPsqlSemFonte("TRIGGER", gatilho.Nome);

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

    private static bool PossuiFontePsql(ProcedimentoSchema procedimento) =>
        !string.IsNullOrWhiteSpace(procedimento.SourceSql);

    private static bool PossuiFontePsql(GatilhoSchema gatilho) =>
        !string.IsNullOrWhiteSpace(gatilho.SourceSql);

    private static string GerarComentarioPsqlSemFonte(string tipoObjeto, string? nomeObjeto)
    {
        string nome = string.IsNullOrWhiteSpace(nomeObjeto)
            ? "unknown"
            : Q(nomeObjeto);

        return $"-- [WARNING] {tipoObjeto} {nome} exists in metadata but has no PSQL source; DDL was not generated for this object.";
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
            sb.Append($" {NormalizarDefaultParametroProcedimento(parametro.DefaultSql)}");

        return sb.ToString();
    }

    private static string NormalizarDefaultParametroProcedimento(string defaultSql)
    {
        string texto = defaultSql.Trim();
        if (texto.StartsWith("DEFAULT", StringComparison.OrdinalIgnoreCase))
        {
            texto = texto["DEFAULT".Length..].Trim();
        }

        if (texto.StartsWith("="))
        {
            string valor = texto[1..].Trim();
            return $"= {valor}";
        }

        return $"= {texto}";
    }

    private static void AjustarDefaultsParametrosProcedimentoParaCompatibilidade(IReadOnlyList<ProcedimentoSchema> procedimentos)
    {
        if (procedimentos.Count == 0)
            return;

        var porNome = procedimentos.ToDictionary(p => p.Nome, StringComparer.OrdinalIgnoreCase);
        var menorQuantidadeArgumentos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var procedimentoChamador in procedimentos)
        {
            string fonte = procedimentoChamador.SourceSql ?? string.Empty;
            foreach (var procedimentoAlvo in procedimentos)
            {
                if (string.Equals(procedimentoChamador.Nome, procedimentoAlvo.Nome, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (int qtdArgumentos in ExtrairChamadasQuantidadeArgumentos(fonte, procedimentoAlvo.Nome))
                {
                    if (menorQuantidadeArgumentos.TryGetValue(procedimentoAlvo.Nome, out int atual))
                    {
                        if (qtdArgumentos < atual)
                            menorQuantidadeArgumentos[procedimentoAlvo.Nome] = qtdArgumentos;
                    }
                    else
                    {
                        menorQuantidadeArgumentos[procedimentoAlvo.Nome] = qtdArgumentos;
                    }
                }
            }
        }

        foreach (var item in menorQuantidadeArgumentos)
        {
            if (!porNome.TryGetValue(item.Key, out var procedimento))
                continue;

            int quantidadeParametros = procedimento.ParametrosEntrada.Count;
            if (item.Value >= quantidadeParametros)
                continue;

            for (int i = item.Value; i < quantidadeParametros; i++)
            {
                var parametro = procedimento.ParametrosEntrada[i];
                if (parametro.AceitaNulo && string.IsNullOrWhiteSpace(parametro.DefaultSql))
                    parametro.DefaultSql = "null";
            }
        }
    }

    private static IEnumerable<int> ExtrairChamadasQuantidadeArgumentos(string sourceSql, string nomeProcedimento)
    {
        if (string.IsNullOrWhiteSpace(sourceSql) || string.IsNullOrWhiteSpace(nomeProcedimento))
            yield break;

        string nomeEscapado = Regex.Escape(nomeProcedimento);
        string padrao = $@"\b(?:FROM|JOIN|EXECUTE\s+PROCEDURE)\s+(?:""{nomeEscapado}""|{nomeEscapado})\s*\(";
        foreach (Match match in Regex.Matches(sourceSql, padrao, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            int inicioArgs = match.Index + match.Length;
            if (!TentarExtrairArgumentos(sourceSql, inicioArgs, out string argumentos))
                continue;

            yield return ContarArgumentos(argumentos);
        }
    }

    private static bool TentarExtrairArgumentos(string texto, int inicio, out string argumentos)
    {
        argumentos = string.Empty;
        int profundidade = 1;
        bool emString = false;
        var sb = new StringBuilder();

        for (int i = inicio; i < texto.Length; i++)
        {
            char ch = texto[i];
            if (emString)
            {
                sb.Append(ch);
                if (ch == '\'')
                {
                    if (i + 1 < texto.Length && texto[i + 1] == '\'')
                    {
                        sb.Append(texto[i + 1]);
                        i++;
                    }
                    else
                    {
                        emString = false;
                    }
                }

                continue;
            }

            if (ch == '\'')
            {
                emString = true;
                sb.Append(ch);
                continue;
            }

            if (ch == '(')
            {
                profundidade++;
                sb.Append(ch);
                continue;
            }

            if (ch == ')')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    argumentos = sb.ToString();
                    return true;
                }

                sb.Append(ch);
                continue;
            }

            sb.Append(ch);
        }

        return false;
    }

    private static int ContarArgumentos(string argumentos)
    {
        if (string.IsNullOrWhiteSpace(argumentos))
            return 0;

        int quantidade = 1;
        int profundidade = 0;
        bool emString = false;

        for (int i = 0; i < argumentos.Length; i++)
        {
            char ch = argumentos[i];
            if (emString)
            {
                if (ch == '\'')
                {
                    if (i + 1 < argumentos.Length && argumentos[i + 1] == '\'')
                        i++;
                    else
                        emString = false;
                }

                continue;
            }

            if (ch == '\'')
            {
                emString = true;
                continue;
            }

            if (ch == '(')
            {
                profundidade++;
                continue;
            }

            if (ch == ')')
            {
                if (profundidade > 0)
                    profundidade--;
                continue;
            }

            if (ch == ',' && profundidade == 0)
                quantidade++;
        }

        return quantidade;
    }

    private static IReadOnlyList<ProcedimentoSchema> OrdenarProcedimentosPorDependencia(IEnumerable<ProcedimentoSchema> procedimentos)
    {
        var lista = procedimentos.OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase).ToList();
        if (lista.Count <= 1)
            return lista;

        var porNome = lista.ToDictionary(p => p.Nome, StringComparer.OrdinalIgnoreCase);
        var dependencias = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var dependentes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var grauEntrada = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var procedimento in lista)
        {
            dependencias[procedimento.Nome] = [];
            dependentes[procedimento.Nome] = [];
            grauEntrada[procedimento.Nome] = 0;
        }

        foreach (var procedimento in lista)
        {
            foreach (var dependencia in ExtrairDependenciasProcedimento(procedimento.SourceSql))
            {
                if (!porNome.ContainsKey(dependencia) || string.Equals(dependencia, procedimento.Nome, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!dependencias[procedimento.Nome].Add(dependencia))
                    continue;

                dependentes[dependencia].Add(procedimento.Nome);
                grauEntrada[procedimento.Nome]++;
            }
        }

        var fila = new PriorityQueue<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nome in grauEntrada.Where(kv => kv.Value == 0).Select(kv => kv.Key))
            fila.Enqueue(nome, nome);

        var ordenados = new List<ProcedimentoSchema>(lista.Count);
        while (fila.Count > 0)
        {
            string nome = fila.Dequeue();
            ordenados.Add(porNome[nome]);

            foreach (var dependente in dependentes[nome].OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                grauEntrada[dependente]--;
                if (grauEntrada[dependente] == 0)
                    fila.Enqueue(dependente, dependente);
            }
        }

        if (ordenados.Count == lista.Count)
            return ordenados;

        var restantes = lista
            .Where(p => !ordenados.Any(o => string.Equals(o.Nome, p.Nome, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Nome, StringComparer.OrdinalIgnoreCase);
        ordenados.AddRange(restantes);
        return ordenados;
    }

    private static IEnumerable<string> ExtrairDependenciasProcedimento(string sourceSql)
    {
        if (string.IsNullOrWhiteSpace(sourceSql))
            yield break;

        const string padraoExecuteProcedure = @"EXECUTE\s+PROCEDURE\s+(?:""(?<nomeq>[^""]+)""|(?<nome>[A-Z0-9_$]+))";
        const string padraoSelectFromProcedure = @"\bFROM\s+(?:""(?<nomeq>[^""]+)""|(?<nome>[A-Z0-9_$]+))\s*\(";
        const string padraoSelectFromSemParametros = @"\bFROM\s+(?:""(?<nomeq>[^""]+)""|(?<nome>[A-Z0-9_$]+))\b";
        const string padraoJoinProcedure = @"\bJOIN\s+(?:""(?<nomeq>[^""]+)""|(?<nome>[A-Z0-9_$]+))\s*\(";
        const string padraoJoinSemParametros = @"\bJOIN\s+(?:""(?<nomeq>[^""]+)""|(?<nome>[A-Z0-9_$]+))\b";
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(sourceSql, padraoExecuteProcedure, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string nome = match.Groups["nomeq"].Success
                ? match.Groups["nomeq"].Value
                : match.Groups["nome"].Value;

            if (!string.IsNullOrWhiteSpace(nome))
                nomes.Add(nome.Trim());
        }

        foreach (Match match in Regex.Matches(sourceSql, padraoSelectFromProcedure, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string nome = match.Groups["nomeq"].Success
                ? match.Groups["nomeq"].Value
                : match.Groups["nome"].Value;

            if (!string.IsNullOrWhiteSpace(nome))
                nomes.Add(nome.Trim());
        }

        foreach (Match match in Regex.Matches(sourceSql, padraoSelectFromSemParametros, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string nome = match.Groups["nomeq"].Success
                ? match.Groups["nomeq"].Value
                : match.Groups["nome"].Value;

            if (!string.IsNullOrWhiteSpace(nome))
                nomes.Add(nome.Trim());
        }

        foreach (Match match in Regex.Matches(sourceSql, padraoJoinProcedure, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string nome = match.Groups["nomeq"].Success
                ? match.Groups["nomeq"].Value
                : match.Groups["nome"].Value;

            if (!string.IsNullOrWhiteSpace(nome))
                nomes.Add(nome.Trim());
        }

        foreach (Match match in Regex.Matches(sourceSql, padraoJoinSemParametros, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string nome = match.Groups["nomeq"].Success
                ? match.Groups["nomeq"].Value
                : match.Groups["nome"].Value;

            if (!string.IsNullOrWhiteSpace(nome))
                nomes.Add(nome.Trim());
        }

        foreach (string nome in nomes)
            yield return nome;
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
        if (DeveOmitirNomeConstraint(pk.Nome))
            return $"ALTER TABLE {Q(tabela.Nome)} ADD PRIMARY KEY ({ListaColunas(pk.Colunas)});";

        return $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(pk.Nome)} PRIMARY KEY ({ListaColunas(pk.Colunas)});";
    }

    public static string GerarAddUnique(TabelaSchema tabela, ChaveUnicaSchema unica)
    {
        if (DeveOmitirNomeConstraint(unica.Nome))
            return $"ALTER TABLE {Q(tabela.Nome)} ADD UNIQUE ({ListaColunas(unica.Colunas)});";

        return $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(unica.Nome)} UNIQUE ({ListaColunas(unica.Colunas)});";
    }

    public static string GerarAddCheck(TabelaSchema tabela, RestricaoCheckSchema check)
    {
        string sql = check.CheckSql.Trim();
        if (!sql.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
            sql = $"CHECK ({sql})";

        if (DeveOmitirNomeConstraint(check.Nome))
            return $"ALTER TABLE {Q(tabela.Nome)} ADD {sql};";

        return $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(check.Nome)} {sql};";
    }

    public static string GerarAddFk(TabelaSchema tabela, ChaveEstrangeiraSchema fk)
    {
        string prefixo = DeveOmitirNomeConstraint(fk.Nome)
            ? $"ALTER TABLE {Q(tabela.Nome)} ADD "
            : $"ALTER TABLE {Q(tabela.Nome)} ADD CONSTRAINT {Q(fk.Nome)} ";

        string sql =
            prefixo +
            $"FOREIGN KEY ({ListaColunas(fk.Colunas)}) REFERENCES {Q(fk.TabelaReferencia)} ({ListaColunas(fk.ColunasReferencia)})";

        if (!string.Equals(fk.RegraUpdate, "RESTRICT", StringComparison.OrdinalIgnoreCase))
            sql += $" ON UPDATE {fk.RegraUpdate}";
        if (!string.Equals(fk.RegraDelete, "RESTRICT", StringComparison.OrdinalIgnoreCase))
            sql += $" ON DELETE {fk.RegraDelete}";

        return sql + ";";
    }

    private static bool DeveOmitirNomeConstraint(string nomeConstraint)
    {
        return RegexConstraintInteg.IsMatch(nomeConstraint.Trim());
    }

    public static string GerarCreateIndex(TabelaSchema tabela, IndiceSchema indice)
    {
        string prefixo;
        if (indice.Unico)
        {
            prefixo = "CREATE UNIQUE INDEX";
        }
        else if (indice.Descendente)
        {
            prefixo = "CREATE DESCENDING INDEX";
        }
        else
        {
            prefixo = "CREATE INDEX";
        }

        return $"{prefixo} {Q(indice.Nome)} ON {Q(tabela.Nome)} ({ListaColunas(indice.Colunas)});";
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
