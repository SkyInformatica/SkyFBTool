using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using System.Globalization;

namespace SkyFBTool.Cli.Commands;

public static class DdlAnalyzeCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        bool portugues = CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
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
                case "output":
                    op.Saida = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
            }
        }

        Console.WriteLine(M(portugues, "Starting DDL analysis...", "Iniciando analise de DDL..."));
        var (arquivoJson, arquivoHtml) = await AnalisadorDdlSchema.AnalisarAsync(op);

        Console.WriteLine();
        Console.WriteLine(M(portugues, "Analysis finished.", "Analise concluida."));
        Console.WriteLine($"{M(portugues, "Analysis JSON", "Analise JSON")}: {arquivoJson}");
        Console.WriteLine($"{M(portugues, "Report", "Relatorio")}     : {arquivoHtml}");
    }

    private static string M(bool portugues, string english, string portuguese)
    {
        return portugues ? portuguese : english;
    }
}
