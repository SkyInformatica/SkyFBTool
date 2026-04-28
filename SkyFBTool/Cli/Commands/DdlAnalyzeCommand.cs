using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;

namespace SkyFBTool.Cli.Commands;

public static class DdlAnalyzeCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();
        var op = new OpcoesDdlAnalise();

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "input":
                case "source":
                    op.Entrada = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "database":
                    op.Database = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "databases-batch":
                    op.DatabasesBatch = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "host":
                    op.Host = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "port":
                    op.Porta = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "user":
                    op.Usuario = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "password":
                    op.Senha = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "charset":
                    op.Charset = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "output":
                    op.Saida = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "ignore-table-prefix":
                    op.PrefixosTabelaIgnorados.Add(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "ignore-table-prefixes":
                    var valor = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    foreach (var prefixo in valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        op.PrefixosTabelaIgnorados.Add(prefixo);
                    break;
                case "severity-config":
                    op.ArquivoConfiguracaoSeveridade = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "description":
                    op.Descricao = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                default:
                    throw new ArgumentException(M(
                        idioma,
                        $"Unknown option: --{chave}",
                        $"Opção desconhecida: --{chave}"));
            }
        }

        Console.WriteLine(M(idioma, "Starting DDL analysis...", "Iniciando análise de DDL..."));
        ValidarModoEntrada(op, idioma);
        var bancos = ResolverBancosParaAnalise(op.DatabasesBatch, idioma);

        if (bancos.Count == 0)
        {
            var (arquivoJson, arquivoHtml) = await AnalisadorDdlSchema.AnalisarAsync(op);

            Console.WriteLine();
            Console.WriteLine(M(idioma, "Analysis finished.", "Análise concluída."));
            Console.WriteLine($"{M(idioma, "Analysis JSON", "Análise JSON")}: {arquivoJson}");
            Console.WriteLine($"{M(idioma, "Report", "Relatório")}     : {arquivoHtml}");
            return;
        }

        Console.WriteLine(M(
            idioma,
            $"Database wildcard resolved to {bancos.Count} file(s).",
            $"Wildcard de banco resolveu para {bancos.Count} arquivo(s)."));

        var entradasResumo = new List<EntradaResumoAnaliseDdlLote>(bancos.Count);

        foreach (var banco in bancos)
        {
            var opBanco = ClonarParaBanco(op, banco);
            var (arquivoJson, arquivoHtml) = await AnalisadorDdlSchema.AnalisarAsync(opBanco);

            entradasResumo.Add(new EntradaResumoAnaliseDdlLote
            {
                Banco = banco,
                ArquivoJson = arquivoJson,
                ArquivoHtml = arquivoHtml
            });

            Console.WriteLine();
            Console.WriteLine($"{M(idioma, "Database", "Banco")}     : {banco}");
            Console.WriteLine($"{M(idioma, "Analysis JSON", "Análise JSON")}: {arquivoJson}");
            Console.WriteLine($"{M(idioma, "Report", "Relatório")}     : {arquivoHtml}");
        }

        var (arquivoResumoJson, arquivoResumoHtml) =
            await GeradorResumoAnaliseDdlLote.GerarAsync(entradasResumo, op.Saida, idioma);

        Console.WriteLine();
        Console.WriteLine(M(idioma, "Batch analysis finished.", "Análise em lote concluída."));
        Console.WriteLine($"{M(idioma, "Batch summary JSON", "Resumo do lote JSON")}: {arquivoResumoJson}");
        Console.WriteLine($"{M(idioma, "Batch summary report", "Relatório resumo do lote")}: {arquivoResumoHtml}");
    }

    private static OpcoesDdlAnalise ClonarParaBanco(OpcoesDdlAnalise baseOp, string banco)
    {
        return new OpcoesDdlAnalise
        {
            Entrada = baseOp.Entrada,
            Database = banco,
            DatabasesBatch = string.Empty,
            Host = baseOp.Host,
            Porta = baseOp.Porta,
            Usuario = baseOp.Usuario,
            Senha = baseOp.Senha,
            Charset = baseOp.Charset,
            Saida = ResolverSaidaPorBanco(baseOp.Saida, banco),
            ArquivoConfiguracaoSeveridade = baseOp.ArquivoConfiguracaoSeveridade,
            Descricao = baseOp.Descricao,
            PrefixosTabelaIgnorados = [.. baseOp.PrefixosTabelaIgnorados]
        };
    }

    private static List<string> ResolverBancosParaAnalise(string databasesBatch, IdiomaSaida idioma)
    {
        if (string.IsNullOrWhiteSpace(databasesBatch))
            return [];

        if (!ContemWildcard(databasesBatch))
        {
            throw new ArgumentException(M(
                idioma,
                "Invalid value for --databases-batch. Use a wildcard pattern (for example: C:\\data\\*.fdb).",
                "Valor inválido para --databases-batch. Use um padrão wildcard (exemplo: C:\\dados\\*.fdb)."));
        }

        string caminho = databasesBatch.Trim().Trim('"');
        string diretorio = Path.GetDirectoryName(caminho) ?? Directory.GetCurrentDirectory();
        string padrao = Path.GetFileName(caminho);

        if (string.IsNullOrWhiteSpace(padrao))
            throw new ArgumentException(M(idioma, "Invalid database wildcard pattern.", "Padrão wildcard de banco inválido."));

        if (!Directory.Exists(diretorio))
        {
            throw new DirectoryNotFoundException(M(
                idioma,
                $"Database directory not found: {diretorio}",
                $"Diretório de bancos não encontrado: {diretorio}"));
        }

        var arquivos = Directory
            .GetFiles(diretorio, padrao)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (arquivos.Count == 0)
        {
            throw new FileNotFoundException(M(
                idioma,
                $"No database file matches pattern: {databasesBatch}",
                $"Nenhum banco corresponde ao padrão: {databasesBatch}"));
        }

        return arquivos;
    }

    private static void ValidarModoEntrada(OpcoesDdlAnalise op, IdiomaSaida idioma)
    {
        bool temArquivo = !string.IsNullOrWhiteSpace(op.Entrada);
        bool temBanco = !string.IsNullOrWhiteSpace(op.Database);
        bool temLote = !string.IsNullOrWhiteSpace(op.DatabasesBatch);

        int total = (temArquivo ? 1 : 0) + (temBanco ? 1 : 0) + (temLote ? 1 : 0);
        if (total == 0)
        {
            throw new ArgumentException(M(
                idioma,
                "Provide one input source: --input/--source, --database, or --databases-batch.",
                "Informe uma origem de entrada: --input/--source, --database ou --databases-batch."));
        }

        if (total > 1)
        {
            throw new ArgumentException(M(
                idioma,
                "Do not combine --input/--source, --database, and --databases-batch. Choose only one source.",
                "Não combine --input/--source, --database e --databases-batch. Escolha apenas uma origem."));
        }

        if (temBanco && ContemWildcard(op.Database))
        {
            throw new ArgumentException(M(
                idioma,
                "Wildcard in --database is not allowed. Use --databases-batch for batch mode.",
                "Wildcard em --database não é permitido. Use --databases-batch para modo em lote."));
        }
    }

    private static bool ContemWildcard(string valor)
    {
        return valor.Contains('*') || valor.Contains('?');
    }

    private static string? ResolverSaidaPorBanco(string? saidaBase, string banco)
    {
        if (string.IsNullOrWhiteSpace(saidaBase))
            return null;

        string dbNome = SanitizarNomeArquivo(Path.GetFileNameWithoutExtension(banco));
        string saida = saidaBase.Trim();

        if (Directory.Exists(saida) ||
            saida.EndsWith(Path.DirectorySeparatorChar) ||
            saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string dir = Path.GetFullPath(saida);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"{dbNome}_analysis");
        }

        string absoluto = Path.GetFullPath(saida);
        string dirSaida = Path.GetDirectoryName(absoluto) ?? Directory.GetCurrentDirectory();
        string nomeSemExt = Path.GetFileNameWithoutExtension(absoluto);
        return Path.Combine(dirSaida, $"{nomeSemExt}_{dbNome}");
    }

    private static string SanitizarNomeArquivo(string valor)
    {
        string nome = string.IsNullOrWhiteSpace(valor) ? "database" : valor;
        foreach (char invalido in Path.GetInvalidFileNameChars())
            nome = nome.Replace(invalido, '_');
        return nome;
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}
