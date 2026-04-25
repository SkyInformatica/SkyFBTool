using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using SkyFBTool.Services.Import;

namespace SkyFBTool.Cli.Commands;

public static class ImportCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();
        var op = new OpcoesImportacao();

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "database":
                    op.Database = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "input":
                case "script":
                    op.ArquivoEntrada = CliArgumentParser.LerValorOpcao(args, ref i, chave);
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
                case "progress-every":
                    op.ProgressoACada = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "continue-on-error":
                    op.ContinuarEmCasoDeErro = true;
                    break;
                default:
                    throw new ArgumentException(M(
                        idioma,
                        $"Unknown option: --{chave}",
                        $"Opcao desconhecida: --{chave}"));
            }
        }

        Console.WriteLine("Iniciando importação...");
        await ImportadorSql.ImportarAsync(op);
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}
