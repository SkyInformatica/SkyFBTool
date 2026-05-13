using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Services.Import;

namespace SkyFBTool.Cli.Commands;

public static class CreateDatabaseCommand
{
    public static async Task ExecuteAsync(string[] args)
    {
        var idioma = IdiomaSaidaDetector.Detectar();
        var op = new OpcoesCriacaoBanco();

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "database":
                    op.Database = CliArgumentParser.LerValorOpcao(args, ref i, chave);
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
                case "page-size":
                    op.PageSize = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "forced-writes":
                    op.ForcedWrites = LerLigaDesliga(CliArgumentParser.LerValorOpcao(args, ref i, chave), chave, idioma);
                    break;
                case "overwrite":
                    op.Overwrite = true;
                    break;
                case "ddl-file":
                    op.ArquivoDdl = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                default:
                    throw new ArgumentException(TextoLocalizado.Obter(
                        idioma,
                        $"Unknown option: --{chave}",
                        $"Opção desconhecida: --{chave}"));
            }
        }

        if (string.IsNullOrWhiteSpace(op.Database))
        {
            throw new ArgumentException(TextoLocalizado.Obter(
                idioma,
                "Option --database is required.",
                "A opção --database é obrigatória."));
        }

        string caminhoBanco = Path.GetFullPath(op.Database.Trim());
        string? diretorio = Path.GetDirectoryName(caminhoBanco);
        if (string.IsNullOrWhiteSpace(diretorio))
        {
            throw new ArgumentException(TextoLocalizado.Obter(
                idioma,
                "Invalid database path.",
                "Caminho de banco inválido."));
        }

        Directory.CreateDirectory(diretorio);

        if (File.Exists(caminhoBanco) && !op.Overwrite)
        {
            throw new InvalidOperationException(TextoLocalizado.Obter(
                idioma,
                $"Database file already exists: {caminhoBanco}. Use --overwrite to recreate it.",
                $"Arquivo de banco já existe: {caminhoBanco}. Use --overwrite para recriá-lo."));
        }

        string? arquivoDdl = null;
        if (!string.IsNullOrWhiteSpace(op.ArquivoDdl))
        {
            arquivoDdl = Path.GetFullPath(op.ArquivoDdl.Trim());
            if (!File.Exists(arquivoDdl))
            {
                throw new FileNotFoundException(
                    TextoLocalizado.Obter(
                        idioma,
                        $"DDL file not found: {arquivoDdl}",
                        $"Arquivo DDL não encontrado: {arquivoDdl}"),
                    arquivoDdl);
            }
        }

        var csb = new FbConnectionStringBuilder
        {
            DataSource = op.Host,
            Port = op.Porta,
            UserID = op.Usuario,
            Password = op.Senha,
            Database = caminhoBanco,
            Charset = op.Charset,
            Dialect = 3
        };

        Console.WriteLine(TextoLocalizado.Obter(idioma, "Starting database creation...", "Iniciando criação do banco..."));

        FbConnection.CreateDatabase(
            csb.ConnectionString,
            pageSize: op.PageSize,
            forcedWrites: op.ForcedWrites,
            overwrite: op.Overwrite);

        Console.WriteLine(TextoLocalizado.Obter(idioma, "Database created successfully.", "Banco criado com sucesso."));
        Console.WriteLine($"{TextoLocalizado.Obter(idioma, "Path", "Caminho")}: {caminhoBanco}");
        Console.WriteLine($"{TextoLocalizado.Obter(idioma, "Charset", "Charset")}: {op.Charset}");
        Console.WriteLine($"{TextoLocalizado.Obter(idioma, "Page size", "Tamanho da página")}: {op.PageSize}");
        Console.WriteLine($"{TextoLocalizado.Obter(idioma, "Forced writes", "Escritas forçadas")}: {(op.ForcedWrites ? TextoLocalizado.Obter(idioma, "on", "ligado") : TextoLocalizado.Obter(idioma, "off", "desligado"))}");

        if (!string.IsNullOrWhiteSpace(arquivoDdl))
        {
            Console.WriteLine(TextoLocalizado.Obter(idioma, "Applying DDL script...", "Aplicando script DDL..."));

            var opImportacao = new OpcoesImportacao
            {
                Database = caminhoBanco,
                Host = op.Host,
                Porta = op.Porta,
                Usuario = op.Usuario,
                Senha = op.Senha,
                ArquivoEntrada = arquivoDdl,
                ContinuarEmCasoDeErro = false
            };

            await ImportadorSql.ImportarAsync(opImportacao, idioma);

            Console.WriteLine(TextoLocalizado.Obter(idioma, "DDL script applied successfully.", "Script DDL aplicado com sucesso."));
        }
    }

    private static bool LerLigaDesliga(string valor, string chave, IdiomaSaida idioma)
    {
        if (valor.Equals("on", StringComparison.OrdinalIgnoreCase))
            return true;
        if (valor.Equals("off", StringComparison.OrdinalIgnoreCase))
            return false;

        throw new ArgumentException(TextoLocalizado.Obter(
            idioma,
            $"Invalid value for --{chave}. Use on or off.",
            $"Valor inválido para --{chave}. Use on ou off."));
    }
}
