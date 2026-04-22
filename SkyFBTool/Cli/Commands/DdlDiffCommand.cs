using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;

namespace SkyFBTool.Cli.Commands;

public static class DdlDiffCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
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

        Console.WriteLine("Iniciando comparação de DDL...");
        var (arquivoSql, arquivoJson, arquivoMarkdown) = await ComparadorSchema.CompararAsync(op);

        Console.WriteLine();
        Console.WriteLine("Comparação concluída.");
        Console.WriteLine($"Diff SQL   : {arquivoSql}");
        Console.WriteLine($"Diff JSON  : {arquivoJson}");
        Console.WriteLine($"Relatório  : {arquivoMarkdown}");
    }
}
