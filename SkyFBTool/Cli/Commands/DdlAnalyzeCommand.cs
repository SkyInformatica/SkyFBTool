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
                default:
                    throw new ArgumentException(M(
                        idioma,
                        $"Unknown option: --{chave}",
                        $"Opcao desconhecida: --{chave}"));
            }
        }

        Console.WriteLine(M(idioma, "Starting DDL analysis...", "Iniciando analise de DDL..."));
        var (arquivoJson, arquivoHtml) = await AnalisadorDdlSchema.AnalisarAsync(op);

        Console.WriteLine();
        Console.WriteLine(M(idioma, "Analysis finished.", "Analise concluida."));
        Console.WriteLine($"{M(idioma, "Analysis JSON", "Analise JSON")}: {arquivoJson}");
        Console.WriteLine($"{M(idioma, "Report", "Relatorio")}     : {arquivoHtml}");
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}
