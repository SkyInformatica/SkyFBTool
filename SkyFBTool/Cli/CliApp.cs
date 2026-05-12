using System.Text;
using SkyFBTool.Cli.Commands;
using SkyFBTool.Cli.Common;

namespace SkyFBTool.Cli;

public static class CliApp
{
    public static async Task RunAsync(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            args = CliArgumentParser.NormalizarArgs(args);

            if (args.Length == 0)
            {
                ExibirResumoComandos();
                return;
            }

            string comando = args[0].ToLowerInvariant();
            string[] argsComando = args.Skip(1).ToArray();
            bool pediuAjudaGlobal = comando is "--help" or "-h" or "help";
            if (pediuAjudaGlobal)
            {
                ExibirResumoComandos();
                return;
            }

            bool pediuAjudaComando = argsComando.Contains("--help", StringComparer.OrdinalIgnoreCase)
                                     || argsComando.Contains("-h", StringComparer.OrdinalIgnoreCase);
            if (pediuAjudaComando)
            {
                ExibirAjudaComando(comando);
                return;
            }

            switch (comando)
            {
                case "export":
                    await ExportCommand.ExecuteAsync(argsComando);
                    break;
                case "create-db":
                    await CreateDatabaseCommand.ExecuteAsync(argsComando);
                    break;
                case "import":
                case "exec-sql":
                    await ImportCommand.ExecuteAsync(argsComando);
                    break;
                case "ddl-extract":
                    await DdlExtractCommand.ExecuteAsync(argsComando);
                    break;
                case "ddl-diff":
                    await DdlDiffCommand.ExecuteAsync(argsComando);
                    break;
                case "ddl-analyze":
                    await DdlAnalyzeCommand.ExecuteAsync(argsComando);
                    break;
                default:
                    Console.WriteLine($"Comando desconhecido: {comando}");
                    ExibirResumoComandos();
                    Environment.ExitCode = 1;
                    break;
            }
        }
        catch (Exception ex)
        {
            CliErrorHandler.Exibir(ex);
            Environment.ExitCode = 1;
        }
    }

    private static void ExibirResumoComandos() => Console.WriteLine(CliHelpText.ObterResumoComandos());

    private static void ExibirAjudaComando(string comando)
    {
        string? ajuda = CliHelpText.ObterAjudaComando(comando);
        if (string.IsNullOrWhiteSpace(ajuda))
        {
            Console.WriteLine($"Comando desconhecido: {comando}");
            ExibirResumoComandos();
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine(ajuda);
    }
}
