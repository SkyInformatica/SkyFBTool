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
        var opBase = new OpcoesImportacao();
        string? padraoArquivosLote = null;

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "database":
                    opBase.Database = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "input":
                case "script":
                    opBase.ArquivoEntrada = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "inputs-batch":
                case "input-batch":
                case "scripts-batch":
                    padraoArquivosLote = CliArgumentParser.LerValorOpcao(args, ref i, chave);
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
                    throw new ArgumentException(CliText.Texto(
                        idioma,
                        $"Unknown option: --{chave}",
                        $"Opção desconhecida: --{chave}"));
            }
        }

        if (!string.IsNullOrWhiteSpace(opBase.ArquivoEntrada) && !string.IsNullOrWhiteSpace(padraoArquivosLote))
        {
            throw new ArgumentException(CliText.Texto(
                idioma,
                "Use only one input mode: --input/--script or --inputs-batch.",
                "Use apenas um modo de entrada: --input/--script ou --inputs-batch."));
        }

        if (!string.IsNullOrWhiteSpace(padraoArquivosLote))
        {
            await ExecutarLoteAsync(opBase, padraoArquivosLote, idioma);
            return;
        }

        Console.WriteLine("Iniciando importação...");
        await ImportadorSql.ImportarAsync(opBase);
    }

    private static async Task ExecutarLoteAsync(OpcoesImportacao opBase, string padraoArquivos, IdiomaSaida idioma)
    {
        var arquivos = ResolverArquivosBatch(padraoArquivos, idioma);
        Console.WriteLine(CliText.Texto(
            idioma,
            $"Input batch resolved to {arquivos.Count} file(s).",
            $"Lote de entrada resolveu para {arquivos.Count} arquivo(s)."));

        int sucesso = 0;
        int sucessoComErros = 0;
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
                Console.WriteLine($"{CliText.Texto(idioma, "Batch file", "Arquivo do lote")}: {arquivo}");
                var resultado = await ImportadorSql.ImportarAsync(op);
                if (resultado.HouveErros)
                    sucessoComErros++;
                else
                    sucesso++;
            }
            catch (Exception ex)
            {
                falha++;
                if (!opBase.ContinuarEmCasoDeErro)
                    throw;

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(CliText.Texto(
                    idioma,
                    $"Failed to import file: {arquivo}",
                    $"Falha ao importar arquivo: {arquivo}"));
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.WriteLine(CliText.Texto(idioma, "Batch import finished.", "Importação em lote concluída."));
        Console.WriteLine($"{CliText.Texto(idioma, "Succeeded", "Sucesso")}: {sucesso}");
        Console.WriteLine($"{CliText.Texto(idioma, "Succeeded with errors", "Sucesso com erros")}: {sucessoComErros}");
        Console.WriteLine($"{CliText.Texto(idioma, "Failed", "Falha")}: {falha}");
        Console.WriteLine($"{CliText.Texto(idioma, "Total files", "Total de arquivos")}: {arquivos.Count}");
    }

    private static List<string> ResolverArquivosBatch(string padraoBatch, IdiomaSaida idioma)
    {
        return CliBatchPatternResolver.ResolverArquivos(
            padraoBatch,
            idioma,
            "Input directory not found",
            "Diretório de entrada não encontrado",
            "No SQL file matches pattern",
            "Nenhum arquivo SQL corresponde ao padrão");
    }
}
