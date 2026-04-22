using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;

namespace SkyFBTool.Cli.Commands;

public static class DdlExtractCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        var op = new OpcoesDdlExtracao();

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "database":
                    op.Database = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "output":
                    op.Saida = CliArgumentParser.LerValorOpcao(args, ref i, chave);
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
            }
        }

        Console.WriteLine("Iniciando extração de DDL...");
        var (arquivoSql, arquivoJson) = await ExtratorDdlFirebird.ExtrairAsync(op);

        Console.WriteLine();
        Console.WriteLine("Extração concluída.");
        Console.WriteLine($"DDL SQL    : {arquivoSql}");
        Console.WriteLine($"Schema JSON: {arquivoJson}");
    }
}
