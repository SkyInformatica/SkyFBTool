using System.Text;
using System.Text.RegularExpressions;

namespace SkyFBTool.Services.Ddl;

internal static class ParserSqlDdlSnapshot
{
    public static async Task<SnapshotSchema> LerArquivoSqlAsync(string arquivoSql)
    {
        if (!File.Exists(arquivoSql))
            throw new FileNotFoundException($"Arquivo SQL não encontrado: {arquivoSql}");

        var snapshot = new SnapshotSchema();
        var tabelas = new Dictionary<string, TabelaSchema>(StringComparer.OrdinalIgnoreCase);

        await foreach (var comando in EnumerarComandosAsync(arquivoSql))
            ProcessarComando(comando, tabelas);

        snapshot.Tabelas = tabelas.Values
            .OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return snapshot;
    }

    private static async IAsyncEnumerable<string> EnumerarComandosAsync(string arquivoSql)
    {
        using var leitor = new StreamReader(arquivoSql, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        char delimitadorAtual = ';';
        var comandoAtual = new StringBuilder();
        bool dentroString = false;
        bool dentroComentarioBloco = false;
        bool dentroComentarioLinha = false;

        string? linhaOriginal;
        while ((linhaOriginal = await leitor.ReadLineAsync()) is not null)
        {
            dentroComentarioLinha = false;
            string linhaAnalise = linhaOriginal.TrimStart('\uFEFF', '\u200B', '\u00A0', '\u2060', ' ', '\t');

            if (TentarProcessarSetTerm(linhaAnalise, ref delimitadorAtual) && comandoAtual.Length == 0)
                continue;

            int i = 0;
            while (i < linhaOriginal.Length)
            {
                char c = linhaOriginal[i];

                if (dentroComentarioBloco)
                {
                    if (c == '*' && i + 1 < linhaOriginal.Length && linhaOriginal[i + 1] == '/')
                    {
                        dentroComentarioBloco = false;
                        i += 2;
                        continue;
                    }

                    i++;
                    continue;
                }

                if (dentroString)
                {
                    comandoAtual.Append(c);

                    if (c == '\'')
                    {
                        if (i + 1 < linhaOriginal.Length && linhaOriginal[i + 1] == '\'')
                        {
                            comandoAtual.Append(linhaOriginal[i + 1]);
                            i += 2;
                            continue;
                        }

                        dentroString = false;
                    }

                    i++;
                    continue;
                }

                if (c == '-' && i + 1 < linhaOriginal.Length && linhaOriginal[i + 1] == '-')
                {
                    dentroComentarioLinha = true;
                    break;
                }

                if (c == '/' && i + 1 < linhaOriginal.Length && linhaOriginal[i + 1] == '*')
                {
                    dentroComentarioBloco = true;
                    i += 2;
                    continue;
                }

                if (c == '\'')
                {
                    dentroString = true;
                    comandoAtual.Append(c);
                    i++;
                    continue;
                }

                if (c == delimitadorAtual)
                {
                    string comando = comandoAtual.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(comando))
                        yield return comando;

                    comandoAtual.Clear();
                    i++;
                    continue;
                }

                comandoAtual.Append(c);
                i++;
            }

            if (!dentroComentarioBloco && !dentroComentarioLinha)
                comandoAtual.AppendLine();
        }

        string ultimo = comandoAtual.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(ultimo))
            yield return ultimo;
    }

    private static bool TentarProcessarSetTerm(string linha, ref char delimitadorAtual)
    {
        if (!linha.StartsWith("SET TERM", StringComparison.OrdinalIgnoreCase))
            return false;

        var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length < 3)
            return false;

        string token = partes[2].Replace(";", "").Trim();
        if (!string.IsNullOrWhiteSpace(token))
            delimitadorAtual = token[0];

        return true;
    }

    private static void ProcessarComando(string comando, Dictionary<string, TabelaSchema> tabelas)
    {
        string limpo = comando.Trim();
        if (string.IsNullOrWhiteSpace(limpo))
            return;

        if (limpo.StartsWith("SET ", StringComparison.OrdinalIgnoreCase) ||
            limpo.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (TentarProcessarCreateTable(limpo, tabelas))
            return;

        if (TentarProcessarAlterTableConstraint(limpo, tabelas))
            return;

        _ = TentarProcessarCreateIndex(limpo, tabelas);
    }

    private static bool TentarProcessarCreateTable(string sql, Dictionary<string, TabelaSchema> tabelas)
    {
        if (!sql.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            return false;

        int indiceAbreParenteses = EncontrarPrimeiroParentesesForaAspas(sql);
        if (indiceAbreParenteses < 0)
            return false;

        string prefixo = sql[..indiceAbreParenteses].Trim();
        string nomeTabelaToken = prefixo["CREATE TABLE".Length..].Trim();
        string nomeTabela = DesquotarIdentificador(nomeTabelaToken);
        if (string.IsNullOrWhiteSpace(nomeTabela))
            return false;

        int indiceFechaParenteses = EncontrarParentesesFechamento(sql, indiceAbreParenteses);
        if (indiceFechaParenteses <= indiceAbreParenteses)
            return false;

        string corpo = sql[(indiceAbreParenteses + 1)..indiceFechaParenteses];
        var itens = SepararPorVirgulaNoTopo(corpo);

        var tabela = ObterOuCriarTabela(tabelas, nomeTabela);

        foreach (var item in itens)
            ProcessarItemCreateTable(item, tabela);

        return true;
    }

    private static void ProcessarItemCreateTable(string item, TabelaSchema tabela)
    {
        string texto = item.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return;

        if (TentarProcessarConstraintInline(texto, tabela))
            return;

        var (nomeColuna, definicao) = ExtrairNomeEPosfixo(texto);
        if (string.IsNullOrWhiteSpace(nomeColuna) || string.IsNullOrWhiteSpace(definicao))
            return;

        if (tabela.Colunas.Any(c => string.Equals(c.Nome, nomeColuna, StringComparison.OrdinalIgnoreCase)))
            return;

        string definicaoUpper = definicao.ToUpperInvariant();
        bool notNull = Regex.IsMatch(definicaoUpper, @"\bNOT\s+NULL\b", RegexOptions.CultureInvariant);

        int idxComputed = IndexOfIgnoreCase(definicao, "COMPUTED BY");
        if (idxComputed >= 0)
        {
            tabela.Colunas.Add(new ColunaSchema
            {
                Nome = nomeColuna,
                TipoSql = "COMPUTED",
                AceitaNulo = true,
                ComputedBySql = definicao[idxComputed..].Trim()
            });
            return;
        }

        string tipo = ExtrairTipoColuna(definicao);
        if (string.IsNullOrWhiteSpace(tipo))
            tipo = "TYPE_UNKNOWN";

        string? defaultSql = ExtrairDefaultColuna(definicao);

        tabela.Colunas.Add(new ColunaSchema
        {
            Nome = nomeColuna,
            TipoSql = tipo,
            AceitaNulo = !notNull,
            DefaultSql = defaultSql
        });
    }

    private static bool TentarProcessarConstraintInline(string texto, TabelaSchema tabela)
    {
        var pkMatch = Regex.Match(
            texto,
            $"^CONSTRAINT\\s+(?<nome>{PadraoIdentificador})\\s+PRIMARY\\s+KEY\\s*\\((?<cols>.+)\\)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        if (pkMatch.Success)
        {
            tabela.ChavePrimaria = new ChavePrimariaSchema
            {
                Nome = DesquotarIdentificador(pkMatch.Groups["nome"].Value),
                Colunas = ExtrairListaColunas(pkMatch.Groups["cols"].Value)
            };
            return true;
        }

        var fkMatch = Regex.Match(
            texto,
            $"^CONSTRAINT\\s+(?<nome>{PadraoIdentificador})\\s+FOREIGN\\s+KEY\\s*\\((?<cols>.+?)\\)\\s+REFERENCES\\s+(?<ref>{PadraoIdentificador})\\s*\\((?<refcols>.+?)\\)(?<rules>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        if (fkMatch.Success)
        {
            tabela.ChavesEstrangeiras.Add(CriarFk(
                DesquotarIdentificador(fkMatch.Groups["nome"].Value),
                fkMatch.Groups["cols"].Value,
                DesquotarIdentificador(fkMatch.Groups["ref"].Value),
                fkMatch.Groups["refcols"].Value,
                fkMatch.Groups["rules"].Value));
            return true;
        }

        return false;
    }

    private static bool TentarProcessarAlterTableConstraint(string sql, Dictionary<string, TabelaSchema> tabelas)
    {
        var pkMatch = Regex.Match(
            sql,
            $"^ALTER\\s+TABLE\\s+(?<tabela>{PadraoIdentificador})\\s+ADD\\s+CONSTRAINT\\s+(?<nome>{PadraoIdentificador})\\s+PRIMARY\\s+KEY\\s*\\((?<cols>.+?)\\)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        if (pkMatch.Success)
        {
            string nomeTabela = DesquotarIdentificador(pkMatch.Groups["tabela"].Value);
            var tabela = ObterOuCriarTabela(tabelas, nomeTabela);
            tabela.ChavePrimaria = new ChavePrimariaSchema
            {
                Nome = DesquotarIdentificador(pkMatch.Groups["nome"].Value),
                Colunas = ExtrairListaColunas(pkMatch.Groups["cols"].Value)
            };
            return true;
        }

        var fkMatch = Regex.Match(
            sql,
            $"^ALTER\\s+TABLE\\s+(?<tabela>{PadraoIdentificador})\\s+ADD\\s+CONSTRAINT\\s+(?<nome>{PadraoIdentificador})\\s+FOREIGN\\s+KEY\\s*\\((?<cols>.+?)\\)\\s+REFERENCES\\s+(?<ref>{PadraoIdentificador})\\s*\\((?<refcols>.+?)\\)(?<rules>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        if (fkMatch.Success)
        {
            string nomeTabela = DesquotarIdentificador(fkMatch.Groups["tabela"].Value);
            var tabela = ObterOuCriarTabela(tabelas, nomeTabela);
            string nomeFk = DesquotarIdentificador(fkMatch.Groups["nome"].Value);

            tabela.ChavesEstrangeiras.RemoveAll(fk => string.Equals(fk.Nome, nomeFk, StringComparison.OrdinalIgnoreCase));
            tabela.ChavesEstrangeiras.Add(CriarFk(
                nomeFk,
                fkMatch.Groups["cols"].Value,
                DesquotarIdentificador(fkMatch.Groups["ref"].Value),
                fkMatch.Groups["refcols"].Value,
                fkMatch.Groups["rules"].Value));
            return true;
        }

        return false;
    }

    private static bool TentarProcessarCreateIndex(string sql, Dictionary<string, TabelaSchema> tabelas)
    {
        var match = Regex.Match(
            sql,
            $"^CREATE\\s+(?<unique>UNIQUE\\s+)?INDEX\\s+(?<nome>{PadraoIdentificador})\\s+ON\\s+(?<tabela>{PadraoIdentificador})(?<resto>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        if (!match.Success)
            return false;

        string resto = match.Groups["resto"].Value;
        int abre = EncontrarPrimeiroParentesesForaAspas(resto);
        if (abre < 0)
            return false;

        int fecha = EncontrarParentesesFechamento(resto, abre);
        if (fecha <= abre)
            return false;

        string trechoAntesColunas = resto[..abre];
        string colunas = resto[(abre + 1)..fecha];
        bool descendente = trechoAntesColunas.Contains("DESCENDING", StringComparison.OrdinalIgnoreCase);

        string nomeTabela = DesquotarIdentificador(match.Groups["tabela"].Value);
        var tabela = ObterOuCriarTabela(tabelas, nomeTabela);
        string nomeIndice = DesquotarIdentificador(match.Groups["nome"].Value);

        tabela.Indices.RemoveAll(i => string.Equals(i.Nome, nomeIndice, StringComparison.OrdinalIgnoreCase));
        tabela.Indices.Add(new IndiceSchema
        {
            Nome = nomeIndice,
            Unico = !string.IsNullOrWhiteSpace(match.Groups["unique"].Value),
            Descendente = descendente,
            Colunas = ExtrairListaColunas(colunas)
        });

        return true;
    }

    private static ChaveEstrangeiraSchema CriarFk(
        string nome,
        string colunas,
        string tabelaReferencia,
        string colunasReferencia,
        string regras)
    {
        string regraUpdate = ExtrairRegraAcao(regras, "UPDATE") ?? "RESTRICT";
        string regraDelete = ExtrairRegraAcao(regras, "DELETE") ?? "RESTRICT";

        return new ChaveEstrangeiraSchema
        {
            Nome = nome,
            IndiceSuporteNome = nome,
            Colunas = ExtrairListaColunas(colunas),
            TabelaReferencia = tabelaReferencia,
            ColunasReferencia = ExtrairListaColunas(colunasReferencia),
            RegraUpdate = regraUpdate,
            RegraDelete = regraDelete
        };
    }

    private static string? ExtrairRegraAcao(string regras, string acao)
    {
        var match = Regex.Match(
            regras,
            $"ON\\s+{acao}\\s+(?<valor>SET\\s+DEFAULT|SET\\s+NULL|NO\\s+ACTION|CASCADE|RESTRICT)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? NormalizarEspacos(match.Groups["valor"].Value).ToUpperInvariant() : null;
    }

    private static string ExtrairTipoColuna(string definicao)
    {
        string[] marcadores =
        [
            " DEFAULT ",
            " NOT NULL",
            " NULL",
            " CONSTRAINT ",
            " CHECK ",
            " COLLATE ",
            " PRIMARY KEY",
            " REFERENCES "
        ];

        int corte = definicao.Length;
        string upper = definicao.ToUpperInvariant();
        foreach (var marcador in marcadores)
        {
            int idx = upper.IndexOf(marcador, StringComparison.Ordinal);
            if (idx >= 0 && idx < corte)
                corte = idx;
        }

        return definicao[..corte].Trim();
    }

    private static string? ExtrairDefaultColuna(string definicao)
    {
        int idx = IndexOfIgnoreCase(definicao, "DEFAULT ");
        if (idx < 0)
            return null;

        string trecho = definicao[idx..].Trim();
        string upper = trecho.ToUpperInvariant();
        string[] marcadores = [" NOT NULL", " NULL", " CONSTRAINT ", " CHECK ", " COLLATE ", " PRIMARY KEY", " REFERENCES "];
        int corte = trecho.Length;
        foreach (var marcador in marcadores)
        {
            int pos = upper.IndexOf(marcador, StringComparison.Ordinal);
            if (pos >= 0 && pos < corte)
                corte = pos;
        }

        return trecho[..corte].Trim();
    }

    private static (string Nome, string Definicao) ExtrairNomeEPosfixo(string texto)
    {
        string valor = texto.Trim();
        if (string.IsNullOrWhiteSpace(valor))
            return (string.Empty, string.Empty);

        if (valor[0] == '"')
        {
            int fim = EncontrarFechamentoAspas(valor, 0);
            if (fim < 1 || fim >= valor.Length - 1)
                return (string.Empty, string.Empty);

            return (
                DesquotarIdentificador(valor[..(fim + 1)]),
                valor[(fim + 1)..].Trim());
        }

        int i = 0;
        while (i < valor.Length && !char.IsWhiteSpace(valor[i]))
            i++;

        if (i <= 0 || i >= valor.Length)
            return (string.Empty, string.Empty);

        return (DesquotarIdentificador(valor[..i]), valor[i..].Trim());
    }

    private static int EncontrarFechamentoAspas(string valor, int inicioAspas)
    {
        int i = inicioAspas + 1;
        while (i < valor.Length)
        {
            if (valor[i] == '"')
            {
                if (i + 1 < valor.Length && valor[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                return i;
            }

            i++;
        }

        return -1;
    }

    private static List<string> ExtrairListaColunas(string texto)
    {
        return SepararPorVirgulaNoTopo(texto)
            .Select(c => DesquotarIdentificador(c.Trim()))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();
    }

    private static int EncontrarPrimeiroParentesesForaAspas(string texto)
    {
        bool emAspas = false;
        for (int i = 0; i < texto.Length; i++)
        {
            char c = texto[i];
            if (c == '"')
            {
                if (emAspas && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                emAspas = !emAspas;
                continue;
            }

            if (!emAspas && c == '(')
                return i;
        }

        return -1;
    }

    private static int EncontrarParentesesFechamento(string texto, int indiceAbre)
    {
        int nivel = 0;
        bool emAspas = false;
        for (int i = indiceAbre; i < texto.Length; i++)
        {
            char c = texto[i];
            if (c == '"')
            {
                if (emAspas && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                emAspas = !emAspas;
                continue;
            }

            if (emAspas)
                continue;

            if (c == '(')
            {
                nivel++;
                continue;
            }

            if (c == ')')
            {
                nivel--;
                if (nivel == 0)
                    return i;
            }
        }

        return -1;
    }

    private static List<string> SepararPorVirgulaNoTopo(string texto)
    {
        var itens = new List<string>();
        var atual = new StringBuilder();
        int nivelParenteses = 0;
        bool emAspas = false;

        for (int i = 0; i < texto.Length; i++)
        {
            char c = texto[i];
            if (c == '"')
            {
                if (emAspas && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    atual.Append(c);
                    atual.Append(texto[i + 1]);
                    i++;
                    continue;
                }

                emAspas = !emAspas;
                atual.Append(c);
                continue;
            }

            if (!emAspas)
            {
                if (c == '(')
                    nivelParenteses++;
                else if (c == ')' && nivelParenteses > 0)
                    nivelParenteses--;
                else if (c == ',' && nivelParenteses == 0)
                {
                    string item = atual.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(item))
                        itens.Add(item);
                    atual.Clear();
                    continue;
                }
            }

            atual.Append(c);
        }

        string ultimo = atual.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(ultimo))
            itens.Add(ultimo);

        return itens;
    }

    private static TabelaSchema ObterOuCriarTabela(Dictionary<string, TabelaSchema> tabelas, string nomeTabela)
    {
        if (tabelas.TryGetValue(nomeTabela, out var tabela))
            return tabela;

        tabela = new TabelaSchema { Nome = nomeTabela };
        tabelas.Add(nomeTabela, tabela);
        return tabela;
    }

    private static string DesquotarIdentificador(string valor)
    {
        string texto = valor.Trim();
        if (texto.Length >= 2 && texto[0] == '"' && texto[^1] == '"')
            return texto[1..^1].Replace("\"\"", "\"");

        return texto.Trim();
    }

    private static int IndexOfIgnoreCase(string texto, string valor)
    {
        return texto.IndexOf(valor, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizarEspacos(string valor)
    {
        return string.Join(
            ' ',
            valor.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private const string PadraoIdentificador = "(?:\"(?:\"\"|[^\"])+\"|[A-Za-z_][A-Za-z0-9_$]*)";
}

