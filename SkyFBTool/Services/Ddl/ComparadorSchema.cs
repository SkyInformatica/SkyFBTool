using System.Text.Json;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl;

public static class ComparadorSchema
{
    public static async Task<(string ArquivoSql, string ArquivoJson, string ArquivoHtml)> CompararAsync(OpcoesDdlDiff opcoes)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();

        if (string.IsNullOrWhiteSpace(opcoes.Origem))
            throw new ArgumentException(TextoLocalizado.Obter(idioma, "Source file not provided (--source).", "Arquivo de origem não informado (--source)."));
        if (string.IsNullOrWhiteSpace(opcoes.Alvo))
            throw new ArgumentException(TextoLocalizado.Obter(idioma, "Target file not provided (--target).", "Arquivo de alvo não informado (--target)."));

        var (origem, origemArquivo) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(opcoes.Origem);
        var (alvo, alvoArquivo) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(opcoes.Alvo);

        var resultado = GerarDiff(origem, alvo, idioma);
        var (arquivoSql, arquivoJson, arquivoHtml) = ResolverArquivosSaida(opcoes);

        Directory.CreateDirectory(Path.GetDirectoryName(arquivoSql)!);
        await File.WriteAllTextAsync(arquivoSql, MontarSql(resultado, idioma));
        await File.WriteAllTextAsync(arquivoJson, JsonSerializer.Serialize(resultado, JsonOptions));
        await File.WriteAllTextAsync(
            arquivoHtml,
            RenderizadorHtmlDiffDdl.Renderizar(resultado, origemArquivo, alvoArquivo, idioma));

        return (arquivoSql, arquivoJson, arquivoHtml);
    }

    public static ResultadoDiffSchema GerarDiff(
        SnapshotSchema origem,
        SnapshotSchema alvo,
        IdiomaSaida idioma = IdiomaSaida.English)
    {
        var resultado = new ResultadoDiffSchema();

        var tabelasOrigem = origem.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);
        var tabelasAlvo = alvo.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);
        var dominiosOrigem = origem.Dominios.ToDictionary(d => d.Nome, StringComparer.OrdinalIgnoreCase);
        var dominiosAlvo = alvo.Dominios.ToDictionary(d => d.Nome, StringComparer.OrdinalIgnoreCase);
        var sequenciasOrigem = origem.Sequencias.ToDictionary(s => s.Nome, StringComparer.OrdinalIgnoreCase);
        var sequenciasAlvo = alvo.Sequencias.ToDictionary(s => s.Nome, StringComparer.OrdinalIgnoreCase);
        var viewsOrigem = origem.Views.ToDictionary(v => v.Nome, StringComparer.OrdinalIgnoreCase);
        var viewsAlvo = alvo.Views.ToDictionary(v => v.Nome, StringComparer.OrdinalIgnoreCase);

        CompararDominios(dominiosOrigem, dominiosAlvo, resultado, idioma);
        CompararSequencias(sequenciasOrigem, sequenciasAlvo, resultado, idioma);
        CompararViews(viewsOrigem, viewsAlvo, resultado, idioma);

        foreach (var nomeTabela in tabelasOrigem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var tabelaOrigem = tabelasOrigem[nomeTabela];
            if (!tabelasAlvo.TryGetValue(nomeTabela, out var tabelaAlvo))
            {
                resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                    $"Table missing in target: {nomeTabela}",
                    $"Tabela ausente no alvo: {nomeTabela}"));
                resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateTable(tabelaOrigem));

                if (tabelaOrigem.ChavePrimaria is not null)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddPk(tabelaOrigem));
                foreach (var fk in tabelaOrigem.ChavesEstrangeiras)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddFk(tabelaOrigem, fk));
                foreach (var unica in tabelaOrigem.ChavesUnicas)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddUnique(tabelaOrigem, unica));
                foreach (var check in tabelaOrigem.RestricoesCheck)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarAddCheck(tabelaOrigem, check));
                foreach (var indice in tabelaOrigem.Indices)
                    resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateIndex(tabelaOrigem, indice));

                continue;
            }

            CompararTabela(tabelaOrigem, tabelaAlvo, resultado, idioma);
        }

        foreach (var nomeTabelaAlvo in tabelasAlvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!tabelasOrigem.ContainsKey(nomeTabelaAlvo))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"Table exists only in target: {nomeTabelaAlvo}",
                    $"Tabela existe apenas no alvo: {nomeTabelaAlvo}"));
            }
        }

        resultado.ComandosSql = OrdenarComandosSql(resultado.ComandosSql, CalcularProfundidadesDependencias(origem));
        return resultado;
    }

    private static void CompararDominios(
        IReadOnlyDictionary<string, DominioSchema> origem,
        IReadOnlyDictionary<string, DominioSchema> alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        foreach (var nome in origem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var dominioOrigem = origem[nome];
            if (!alvo.TryGetValue(nome, out var dominioAlvo))
            {
                resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                    $"Domain missing in target: {nome}",
                    $"Domínio ausente no alvo: {nome}"));
                resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateDomain(dominioOrigem));
                continue;
            }

            if (DominiosEquivalentes(dominioOrigem, dominioAlvo))
                continue;

            resultado.Avisos.Add(TextoLocalizado.Obter(idioma,
                $"Domain differs and requires manual review: {nome}",
                $"Domínio diferente e requer revisão manual: {nome}"));
        }

        foreach (var nome in alvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!origem.ContainsKey(nome))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"Domain exists only in target: {nome}",
                    $"Domínio existe apenas no alvo: {nome}"));
            }
        }
    }

    private static void CompararSequencias(
        IReadOnlyDictionary<string, SequenciaSchema> origem,
        IReadOnlyDictionary<string, SequenciaSchema> alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        foreach (var nome in origem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (alvo.ContainsKey(nome))
                continue;

            resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                $"Sequence missing in target: {nome}",
                $"Sequência ausente no alvo: {nome}"));
            resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateSequence(origem[nome]));
        }

        foreach (var nome in alvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!origem.ContainsKey(nome))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"Sequence exists only in target: {nome}",
                    $"Sequência existe apenas no alvo: {nome}"));
            }
        }
    }

    private static void CompararViews(
        IReadOnlyDictionary<string, ViewSchema> origem,
        IReadOnlyDictionary<string, ViewSchema> alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        foreach (var nome in origem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var viewOrigem = origem[nome];
            if (!alvo.TryGetValue(nome, out var viewAlvo))
            {
                resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                    $"View missing in target: {nome}",
                    $"View ausente no alvo: {nome}"));
                resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateView(viewOrigem));
                continue;
            }

            if (ViewsEquivalentes(viewOrigem, viewAlvo))
                continue;

            resultado.Avisos.Add(TextoLocalizado.Obter(idioma,
                $"View differs and requires manual review: {nome}",
                $"View diferente e requer revisÃ£o manual: {nome}"));
        }

        foreach (var nome in alvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!origem.ContainsKey(nome))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"View exists only in target: {nome}",
                    $"View existe apenas no alvo: {nome}"));
            }
        }
    }

    private static List<string> OrdenarComandosSql(List<string> comandos, IReadOnlyDictionary<string, int> profundidadesDependencia)
    {
        return comandos
            .Select((comando, indice) => new ComandoPlanejado
            {
                Sql = comando,
                Ordem = ClassificarComandoSql(comando),
                Tabela = ExtrairTabelaComando(comando),
                Profundidade = ExtrairProfundidadeComando(comando, profundidadesDependencia),
                IndiceOriginal = indice
            })
            .OrderBy(c => c.Ordem)
            .ThenBy(c => c.Profundidade)
            .ThenBy(c => c.Tabela, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.IndiceOriginal)
            .Select(c => c.Sql)
            .ToList();
    }

    private static int ClassificarComandoSql(string comando)
    {
        string sql = comando.TrimStart();

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("DROP CONSTRAINT", StringComparison.OrdinalIgnoreCase))
            return 10;

        if (sql.StartsWith("CREATE DOMAIN", StringComparison.OrdinalIgnoreCase))
            return 15;

        if (sql.StartsWith("CREATE SEQUENCE", StringComparison.OrdinalIgnoreCase) ||
            sql.StartsWith("CREATE GENERATOR", StringComparison.OrdinalIgnoreCase))
            return 16;

        if (sql.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            return 20;

        if (sql.StartsWith("CREATE VIEW", StringComparison.OrdinalIgnoreCase))
            return 25;

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains(" ADD ", StringComparison.OrdinalIgnoreCase) &&
            !sql.Contains("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
            return 30;

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("ALTER COLUMN", StringComparison.OrdinalIgnoreCase))
            return 35;

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("ADD CONSTRAINT", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
            return 40;

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("ADD CONSTRAINT", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            return 42;

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("ADD CONSTRAINT", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("CHECK", StringComparison.OrdinalIgnoreCase))
            return 43;

        if (sql.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase) ||
            sql.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase) ||
            sql.StartsWith("CREATE DESCENDING INDEX", StringComparison.OrdinalIgnoreCase) ||
            sql.StartsWith("CREATE UNIQUE DESCENDING INDEX", StringComparison.OrdinalIgnoreCase))
            return 50;

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("ADD CONSTRAINT", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            return 60;

        return 70;
    }

    private static string ExtrairTabelaComando(string comando)
    {
        string sql = comando.TrimStart();
        int inicio;
        int fim;

        if (sql.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
        {
            inicio = sql.IndexOf('"');
            if (inicio < 0) return string.Empty;
            fim = sql.IndexOf('"', inicio + 1);
            if (fim <= inicio) return string.Empty;
            return sql.Substring(inicio + 1, fim - inicio - 1);
        }

        if (sql.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase))
        {
            inicio = sql.IndexOf('"');
            if (inicio < 0) return string.Empty;
            fim = sql.IndexOf('"', inicio + 1);
            if (fim <= inicio) return string.Empty;
            return sql.Substring(inicio + 1, fim - inicio - 1);
        }

        int onIndex = sql.IndexOf(" ON ", StringComparison.OrdinalIgnoreCase);
        if (onIndex >= 0)
        {
            inicio = sql.IndexOf('"', onIndex);
            if (inicio < 0) return string.Empty;
            fim = sql.IndexOf('"', inicio + 1);
            if (fim <= inicio) return string.Empty;
            return sql.Substring(inicio + 1, fim - inicio - 1);
        }

        return string.Empty;
    }

    private sealed class ComandoPlanejado
    {
        public string Sql { get; init; } = string.Empty;
        public int Ordem { get; init; }
        public string Tabela { get; init; } = string.Empty;
        public int Profundidade { get; init; }
        public int IndiceOriginal { get; init; }
    }

    private static IReadOnlyDictionary<string, int> CalcularProfundidadesDependencias(SnapshotSchema snapshot)
    {
        var tabelas = snapshot.Tabelas.ToDictionary(t => t.Nome, StringComparer.OrdinalIgnoreCase);
        var memo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pilha = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int Calcular(string nomeTabela)
        {
            if (memo.TryGetValue(nomeTabela, out int profundidade))
                return profundidade;

            if (!tabelas.TryGetValue(nomeTabela, out var tabela))
                return 0;

            if (!pilha.Add(nomeTabela))
                return 0;

            int maiorDependencia = 0;
            foreach (var fk in tabela.ChavesEstrangeiras)
            {
                if (string.IsNullOrWhiteSpace(fk.TabelaReferencia))
                    continue;

                if (!tabelas.ContainsKey(fk.TabelaReferencia))
                    continue;

                maiorDependencia = Math.Max(maiorDependencia, Calcular(fk.TabelaReferencia) + 1);
            }

            pilha.Remove(nomeTabela);
            memo[nomeTabela] = maiorDependencia;
            return maiorDependencia;
        }

        foreach (var tabela in tabelas.Keys)
            Calcular(tabela);

        return memo;
    }

    private static int ExtrairProfundidadeComando(string comando, IReadOnlyDictionary<string, int> profundidadesDependencia)
    {
        string tabela = ExtrairTabelaComando(comando);
        return string.IsNullOrWhiteSpace(tabela) || !profundidadesDependencia.TryGetValue(tabela, out int profundidade)
            ? 0
            : profundidade;
    }

    private static void CompararTabela(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var colunasOrigem = origem.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        var colunasAlvo = alvo.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var nomeColuna in colunasOrigem.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var colunaOrigem = colunasOrigem[nomeColuna];
            if (!colunasAlvo.TryGetValue(nomeColuna, out var colunaAlvo))
            {
                resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                    $"Column missing in target: {origem.Nome}.{nomeColuna}",
                    $"Coluna ausente no alvo: {origem.Nome}.{nomeColuna}"));
                resultado.ComandosSql.Add(
                    $"ALTER TABLE {GeradorDdlSql.Q(origem.Nome)} ADD {GeradorDdlSql.GerarDefinicaoColuna(colunaOrigem)};");
                continue;
            }

            CompararColuna(origem.Nome, colunaOrigem, colunaAlvo, resultado, idioma);
        }

        foreach (var nomeColunaAlvo in colunasAlvo.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!colunasOrigem.ContainsKey(nomeColunaAlvo))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"Column exists only in target: {origem.Nome}.{nomeColunaAlvo}",
                    $"Coluna existe apenas no alvo: {origem.Nome}.{nomeColunaAlvo}"));
            }
        }

        CompararPk(origem, alvo, resultado, idioma);
        CompararUniqueConstraints(origem, alvo, resultado, idioma);
        CompararChecks(origem, alvo, resultado, idioma);
        CompararFks(origem, alvo, resultado, idioma);
        CompararIndices(origem, alvo, resultado, idioma);
    }

    private static void CompararColuna(
        string nomeTabela,
        ColunaSchema origem,
        ColunaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        if (!string.Equals(origem.ComputedBySql, alvo.ComputedBySql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.Avisos.Add(TextoLocalizado.Obter(idioma,
                $"Computed column differs: {nomeTabela}.{origem.Nome} (manual adjustment recommended).",
                $"Coluna computada diferente: {nomeTabela}.{origem.Nome} (ajuste manual recomendado)."));
            return;
        }

        if (!string.Equals(origem.TipoSql, alvo.TipoSql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.ItensAlterados.Add(TextoLocalizado.Obter(idioma,
                $"Type differs: {nomeTabela}.{origem.Nome} ({alvo.TipoSql} -> {origem.TipoSql})",
                $"Tipo diferente: {nomeTabela}.{origem.Nome} ({alvo.TipoSql} -> {origem.TipoSql})"));
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} TYPE {origem.TipoSql};");
        }

        if (origem.AceitaNulo != alvo.AceitaNulo)
        {
            string nulidadeOrigem = origem.AceitaNulo ? "NULL" : "NOT NULL";
            string nulidadeAlvo = alvo.AceitaNulo ? "NULL" : "NOT NULL";
            resultado.ItensAlterados.Add(TextoLocalizado.Obter(idioma,
                $"Nullability differs: {nomeTabela}.{origem.Nome} (target: {nulidadeAlvo} -> source: {nulidadeOrigem})",
                $"Nulidade diferente: {nomeTabela}.{origem.Nome} (alvo: {nulidadeAlvo} -> origem: {nulidadeOrigem})"));
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} {(origem.AceitaNulo ? "DROP" : "SET")} NOT NULL;");
        }

        if (!string.Equals(origem.DefaultSql, alvo.DefaultSql, StringComparison.OrdinalIgnoreCase))
        {
            resultado.ItensAlterados.Add(TextoLocalizado.Obter(idioma,
                $"Default differs: {nomeTabela}.{origem.Nome}",
                $"Default diferente: {nomeTabela}.{origem.Nome}"));
            if (string.IsNullOrWhiteSpace(origem.DefaultSql))
            {
                resultado.ComandosSql.Add(
                    $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} DROP DEFAULT;");
            }
            else
            {
                resultado.ComandosSql.Add(
                    $"ALTER TABLE {GeradorDdlSql.Q(nomeTabela)} ALTER COLUMN {GeradorDdlSql.Q(origem.Nome)} SET {origem.DefaultSql};");
            }
        }
    }

    private static void CompararPk(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        string assinaturaOrigem = AssinaturaPk(origem.ChavePrimaria);
        string assinaturaAlvo = AssinaturaPk(alvo.ChavePrimaria);
        if (assinaturaOrigem == assinaturaAlvo)
            return;

        string nomePkOrigem = origem.ChavePrimaria?.Nome ?? TextoLocalizado.Obter(idioma, "none", "nenhuma");
        string nomePkAlvo = alvo.ChavePrimaria?.Nome ?? TextoLocalizado.Obter(idioma, "none", "nenhuma");
        string colunasOrigem = origem.ChavePrimaria is null
            ? TextoLocalizado.Obter(idioma, "none", "nenhuma")
            : string.Join(", ", origem.ChavePrimaria.Colunas);
        string colunasAlvo = alvo.ChavePrimaria is null
            ? TextoLocalizado.Obter(idioma, "none", "nenhuma")
            : string.Join(", ", alvo.ChavePrimaria.Colunas);
        resultado.ItensAlterados.Add(TextoLocalizado.Obter(idioma,
            $"PK differs: {origem.Nome} (target: {nomePkAlvo} [{colunasAlvo}] -> source: {nomePkOrigem} [{colunasOrigem}])",
            $"PK diferente: {origem.Nome} (alvo: {nomePkAlvo} [{colunasAlvo}] -> origem: {nomePkOrigem} [{colunasOrigem}])"));
        if (alvo.ChavePrimaria is not null)
        {
            resultado.ComandosSql.Add(
                $"ALTER TABLE {GeradorDdlSql.Q(origem.Nome)} DROP CONSTRAINT {GeradorDdlSql.Q(alvo.ChavePrimaria.Nome)};");
        }

        if (origem.ChavePrimaria is not null)
            resultado.ComandosSql.Add(GeradorDdlSql.GerarAddPk(origem));
    }

    private static void CompararFks(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var fksOrigem = CriarMapaPorAssinatura(
            origem.ChavesEstrangeiras,
            AssinaturaFk,
            fk => fk.Nome,
            resultado,
            TextoLocalizado.Obter(idioma, $"Duplicated FK in source ({origem.Nome})", $"FK duplicada na origem ({origem.Nome})"));
        var fksAlvo = CriarMapaPorAssinatura(
            alvo.ChavesEstrangeiras,
            AssinaturaFk,
            fk => fk.Nome,
            resultado,
            TextoLocalizado.Obter(idioma, $"Duplicated FK in target ({alvo.Nome})", $"FK duplicada no alvo ({alvo.Nome})"));

        foreach (var assinatura in fksOrigem.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (fksAlvo.ContainsKey(assinatura))
                continue;

            resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                $"FK missing in target: {origem.Nome}.{fksOrigem[assinatura].Nome}",
                $"FK ausente no alvo: {origem.Nome}.{fksOrigem[assinatura].Nome}"));
            resultado.ComandosSql.Add(GeradorDdlSql.GerarAddFk(origem, fksOrigem[assinatura]));
        }

        foreach (var assinatura in fksAlvo.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (!fksOrigem.ContainsKey(assinatura))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"FK exists only in target: {origem.Nome}.{fksAlvo[assinatura].Nome}",
                    $"FK existe apenas no alvo: {origem.Nome}.{fksAlvo[assinatura].Nome}"));
            }
        }
    }

    private static void CompararUniqueConstraints(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var origemMap = origem.ChavesUnicas.ToDictionary(u => u.Nome, StringComparer.OrdinalIgnoreCase);
        var alvoMap = alvo.ChavesUnicas.ToDictionary(u => u.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var nome in origemMap.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var unicaOrigem = origemMap[nome];
            if (!alvoMap.TryGetValue(nome, out var unicaAlvo))
            {
                resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                    $"Unique constraint missing in target: {origem.Nome}.{nome}",
                    $"Constraint única ausente no alvo: {origem.Nome}.{nome}"));
                resultado.ComandosSql.Add(GeradorDdlSql.GerarAddUnique(origem, unicaOrigem));
                continue;
            }

            if (string.Equals(AssinaturaUnique(unicaOrigem), AssinaturaUnique(unicaAlvo), StringComparison.OrdinalIgnoreCase))
                continue;

            resultado.Avisos.Add(TextoLocalizado.Obter(idioma,
                $"Unique constraint differs and requires manual review: {origem.Nome}.{nome}",
                $"Constraint única diferente e requer revisão manual: {origem.Nome}.{nome}"));
        }

        foreach (var nome in alvoMap.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!origemMap.ContainsKey(nome))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"Unique constraint exists only in target: {origem.Nome}.{nome}",
                    $"Constraint única existe apenas no alvo: {origem.Nome}.{nome}"));
            }
        }
    }

    private static void CompararChecks(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var origemMap = origem.RestricoesCheck.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        var alvoMap = alvo.RestricoesCheck.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var nome in origemMap.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var checkOrigem = origemMap[nome];
            if (!alvoMap.TryGetValue(nome, out var checkAlvo))
            {
                resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                    $"Check constraint missing in target: {origem.Nome}.{nome}",
                    $"Constraint CHECK ausente no alvo: {origem.Nome}.{nome}"));
                resultado.ComandosSql.Add(GeradorDdlSql.GerarAddCheck(origem, checkOrigem));
                continue;
            }

            if (string.Equals(CheckNormalizado(checkOrigem), CheckNormalizado(checkAlvo), StringComparison.OrdinalIgnoreCase))
                continue;

            resultado.Avisos.Add(TextoLocalizado.Obter(idioma,
                $"Check constraint differs and requires manual review: {origem.Nome}.{nome}",
                $"Constraint CHECK diferente e requer revisão manual: {origem.Nome}.{nome}"));
        }

        foreach (var nome in alvoMap.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!origemMap.ContainsKey(nome))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"Check constraint exists only in target: {origem.Nome}.{nome}",
                    $"Constraint CHECK existe apenas no alvo: {origem.Nome}.{nome}"));
            }
        }
    }

    private static void CompararIndices(
        TabelaSchema origem,
        TabelaSchema alvo,
        ResultadoDiffSchema resultado,
        IdiomaSaida idioma)
    {
        var idxOrigem = CriarMapaPorAssinatura(
            origem.Indices,
            AssinaturaIndice,
            idx => idx.Nome,
            resultado,
            TextoLocalizado.Obter(idioma, $"Duplicated index in source ({origem.Nome})", $"Índice duplicado na origem ({origem.Nome})"));
        var idxAlvo = CriarMapaPorAssinatura(
            alvo.Indices,
            AssinaturaIndice,
            idx => idx.Nome,
            resultado,
            TextoLocalizado.Obter(idioma, $"Duplicated index in target ({alvo.Nome})", $"Índice duplicado no alvo ({alvo.Nome})"));

        foreach (var assinatura in idxOrigem.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (idxAlvo.ContainsKey(assinatura))
                continue;

            resultado.ItensCriados.Add(TextoLocalizado.Obter(idioma,
                $"Index missing in target: {origem.Nome}.{idxOrigem[assinatura].Nome}",
                $"Índice ausente no alvo: {origem.Nome}.{idxOrigem[assinatura].Nome}"));
            resultado.ComandosSql.Add(GeradorDdlSql.GerarCreateIndex(origem, idxOrigem[assinatura]));
        }

        foreach (var assinatura in idxAlvo.Keys.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (!idxOrigem.ContainsKey(assinatura))
            {
                resultado.ItensSomenteNoAlvo.Add(TextoLocalizado.Obter(idioma,
                    $"Index exists only in target: {origem.Nome}.{idxAlvo[assinatura].Nome}",
                    $"Índice existe apenas no alvo: {origem.Nome}.{idxAlvo[assinatura].Nome}"));
            }
        }
    }

    private static string AssinaturaPk(ChavePrimariaSchema? pk)
    {
        if (pk is null)
            return string.Empty;
        return string.Join("|", pk.Colunas).ToUpperInvariant();
    }

    private static string AssinaturaFk(ChaveEstrangeiraSchema fk)
    {
        return string.Join("|",
                   fk.Colunas).ToUpperInvariant() +
               "->" + fk.TabelaReferencia.ToUpperInvariant() +
               "(" + string.Join("|", fk.ColunasReferencia).ToUpperInvariant() + ")" +
               $"[{fk.RegraUpdate.ToUpperInvariant()}|{fk.RegraDelete.ToUpperInvariant()}]";
    }

    private static string AssinaturaIndice(IndiceSchema idx)
    {
        return $"{(idx.Unico ? "U" : "N")}|{(idx.Descendente ? "D" : "A")}|{string.Join("|", idx.Colunas).ToUpperInvariant()}";
    }

    private static string AssinaturaUnique(ChaveUnicaSchema unica)
    {
        return string.Join("|", unica.Colunas).ToUpperInvariant();
    }

    private static string CheckNormalizado(RestricaoCheckSchema check)
    {
        return string.Join(" ", check.CheckSql.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToUpperInvariant();
    }

    private static bool ViewsEquivalentes(ViewSchema origem, ViewSchema alvo)
    {
        return string.Equals(ViewSqlNormalizada(origem.SelectSql), ViewSqlNormalizada(alvo.SelectSql), StringComparison.OrdinalIgnoreCase);
    }

    private static string ViewSqlNormalizada(string sql)
    {
        return string.Join(
                ' ',
                sql.Split(new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToUpperInvariant();
    }

    private static bool DominiosEquivalentes(DominioSchema origem, DominioSchema alvo)
    {
        return string.Equals(origem.TipoSql, alvo.TipoSql, StringComparison.OrdinalIgnoreCase)
               && origem.AceitaNulo == alvo.AceitaNulo
               && string.Equals(origem.DefaultSql, alvo.DefaultSql, StringComparison.OrdinalIgnoreCase)
               && string.Equals(origem.CheckSql, alvo.CheckSql, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, T> CriarMapaPorAssinatura<T>(
        IEnumerable<T> itens,
        Func<T, string> assinatura,
        Func<T, string>? nome,
        ResultadoDiffSchema resultado,
        string contextoAviso)
    {
        var mapa = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in itens)
        {
            string chave = assinatura(item);
            if (mapa.ContainsKey(chave))
            {
                string nomeAtual = NomeItem(nome, item);
                string nomeExistente = NomeItem(nome, mapa[chave]);
                resultado.Avisos.Add($"{contextoAviso}: {nomeExistente} <-> {nomeAtual} | {chave}");
                continue;
            }

            mapa.Add(chave, item);
        }

        return mapa;
    }

    private static string NomeItem<T>(Func<T, string>? resolverNome, T item)
    {
        if (resolverNome is null)
            return "?";

        string nome = resolverNome(item) ?? string.Empty;
        return string.IsNullOrWhiteSpace(nome) ? "?" : nome.Trim();
    }

    private static (string ArquivoSql, string ArquivoJson, string ArquivoHtml) ResolverArquivosSaida(OpcoesDdlDiff opcoes)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string padrao = $"schema_diff_{timestamp}";

        if (string.IsNullOrWhiteSpace(opcoes.Saida))
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), padrao);
            return ($"{basePath}.sql", $"{basePath}.json", $"{basePath}.html");
        }

        string saida = opcoes.Saida.Trim();
        if (Directory.Exists(saida) || saida.EndsWith(Path.DirectorySeparatorChar) || saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string basePath = Path.Combine(Path.GetFullPath(saida), padrao);
            return ($"{basePath}.sql", $"{basePath}.json", $"{basePath}.html");
        }

        string semExtensao = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(saida)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(saida));

        return ($"{semExtensao}.sql", $"{semExtensao}.json", $"{semExtensao}.html");
    }

    private static string MontarSql(ResultadoDiffSchema resultado, IdiomaSaida idioma)
    {
        if (resultado.ComandosSql.Count == 0)
        {
            return TextoLocalizado.Obter(idioma,
                       "-- No differences requiring automatic SQL generation.",
                       "-- Nenhuma diferenca que exija SQL automatico.")
                   + Environment.NewLine;
        }

        return string.Join(Environment.NewLine, resultado.ComandosSql) + Environment.NewLine;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

