using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;

namespace SkyFBTool.Cli.Commands;

public static class DdlDiffCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        var idioma = IdiomaSaidaDetector.Detectar();
        var op = new OpcoesDdlDiff();

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "source":
                case "source-ddl":
                    op.Origem = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "target":
                case "target-ddl":
                    op.Alvo = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "output":
                    op.Saida = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
            }
        }

        Console.WriteLine(M(idioma, "Starting DDL comparison...", "Iniciando comparacao de DDL..."));
        var (arquivoSql, arquivoJson, arquivoHtml) = await ComparadorSchema.CompararAsync(op);

        Console.WriteLine();
        Console.WriteLine(M(idioma, "Comparison finished.", "Comparacao concluida."));
        Console.WriteLine($"{M(idioma, "Diff SQL", "Diff SQL")}   : {arquivoSql}");
        Console.WriteLine($"{M(idioma, "Diff JSON", "Diff JSON")}  : {arquivoJson}");
        Console.WriteLine($"{M(idioma, "Report", "Relatorio")}     : {arquivoHtml}");
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}
