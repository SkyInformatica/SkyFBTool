using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using SkyFBTool.Services.Import;

namespace SkyFBTool.Cli.Commands;

public static class ImportBatchCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        var idioma = IdiomaSaidaDetector.Detectar();
        var opBase = new OpcoesImportacao();
        string? padraoArquivos = null;

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "database":
                    opBase.Database = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "inputs-batch":
                case "input-batch":
                case "scripts-batch":
                    padraoArquivos = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "host":
                    opBase.Host = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "port":
                    opBase.Porta = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "user":
                    opBase.Usuario = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "password":
                    opBase.Senha = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "progress-every":
                    opBase.ProgressoACada = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "continue-on-error":
                    opBase.ContinuarEmCasoDeErro = true;
                    break;
                default:
                    throw new ArgumentException(M(
                        idioma,
                        $"Unknown option: --{chave}",
                        $"Opção desconhecida: --{chave}"));
            }
        }

        if (string.IsNullOrWhiteSpace(padraoArquivos))
        {
            throw new ArgumentException(M(
                idioma,
                "Input batch pattern not provided (--inputs-batch).",
                "Padrão de lote de entrada não informado (--inputs-batch)."));
        }

        var arquivos = ResolverArquivosBatch(padraoArquivos, idioma);
        Console.WriteLine(M(
            idioma,
            $"Input batch resolved to {arquivos.Count} file(s).",
            $"Lote de entrada resolveu para {arquivos.Count} arquivo(s)."));

        int sucesso = 0;
        int falha = 0;

        foreach (var arquivo in arquivos)
        {
            var op = new OpcoesImportacao
            {
                Host = opBase.Host,
                Porta = opBase.Porta,
                Database = opBase.Database,
                Usuario = opBase.Usuario,
                Senha = opBase.Senha,
                ArquivoEntrada = arquivo,
                ProgressoACada = opBase.ProgressoACada,
                ContinuarEmCasoDeErro = opBase.ContinuarEmCasoDeErro
            };

            try
            {
                Console.WriteLine();
                Console.WriteLine($"{M(idioma, "Batch file", "Arquivo do lote")}: {arquivo}");
                await ImportadorSql.ImportarAsync(op);
                sucesso++;
            }
            catch (Exception ex)
            {
                falha++;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(M(
                    idioma,
                    $"Failed to import file: {arquivo}",
                    $"Falha ao importar arquivo: {arquivo}"));
                Console.WriteLine(ex.Message);
                Console.ResetColor();

                if (!opBase.ContinuarEmCasoDeErro)
                    throw;
            }
        }

        Console.WriteLine();
        Console.WriteLine(M(idioma, "Batch import finished.", "Importação em lote concluída."));
        Console.WriteLine($"{M(idioma, "Succeeded", "Sucesso")}: {sucesso}");
        Console.WriteLine($"{M(idioma, "Failed", "Falha")}: {falha}");
        Console.WriteLine($"{M(idioma, "Total files", "Total de arquivos")}: {arquivos.Count}");
    }

    private static List<string> ResolverArquivosBatch(string padraoBatch, IdiomaSaida idioma)
    {
        string caminho = padraoBatch.Trim().Trim('"');
        string diretorio = Path.GetDirectoryName(caminho) ?? Directory.GetCurrentDirectory();
        string padrao = Path.GetFileName(caminho);

        if (string.IsNullOrWhiteSpace(padrao))
        {
            throw new ArgumentException(M(
                idioma,
                "Invalid input batch pattern.",
                "Padrão de lote de entrada inválido."));
        }

        if (!Directory.Exists(diretorio))
        {
            throw new DirectoryNotFoundException(M(
                idioma,
                $"Input directory not found: {diretorio}",
                $"Diretório de entrada não encontrado: {diretorio}"));
        }

        var arquivos = Directory
            .GetFiles(diretorio, padrao)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (arquivos.Count == 0)
        {
            throw new FileNotFoundException(M(
                idioma,
                $"No SQL file matches pattern: {padraoBatch}",
                $"Nenhum arquivo SQL corresponde ao padrão: {padraoBatch}"));
        }

        return arquivos;
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}
