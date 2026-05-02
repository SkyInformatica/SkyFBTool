using SkyFBTool.Core;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Services.Ddl;
using SkyFBTool.Services.Import;

namespace SkyFBTool.Cli.Common;

public static class CliErrorHandler
{
    public static void Exibir(Exception ex)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(TextoLocalizado.Obter(idioma, "Execution failed.", "Execução falhou."));
        Console.ResetColor();

        if (ex is not FalhaExtracaoDdlException && TentarExibirErroOdsIncompativel(ex, idioma))
            return;

        switch (ex)
        {
            case FalhaExtracaoDdlException falhaExtracao:
                ExibirFalhaExtracaoDdl(falhaExtracao, idioma);
                break;
            case FalhaImportacaoSqlException falhaImportacao:
                Console.Error.WriteLine(TextoLocalizado.Obter(idioma,
                    "Import failed while executing a SQL command.",
                    "A importação falhou ao executar um comando SQL."));
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "File", "Arquivo")}: {falhaImportacao.Arquivo}");
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Command start line", "Linha inicial do comando")}: {falhaImportacao.LinhaInicioComando}");
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Command preview", "Prévia do comando")}: {GerarPreviaComando(falhaImportacao.ComandoSql)}");
                if (falhaImportacao.InnerException is FbException fbInternoImport)
                    Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Firebird error:", "Erro do Firebird:")} {fbInternoImport.Message}");
                else if (falhaImportacao.InnerException is not null)
                    Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Error:", "Erro:")} {falhaImportacao.InnerException.Message}");
                break;
            case ArgumentException arg:
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Invalid arguments:", "Argumentos inválidos:")} {arg.Message}");
                break;
            case FileNotFoundException fnf:
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "File not found:", "Arquivo não encontrado:")} {fnf.Message}");
                break;
            case DirectoryNotFoundException dnf:
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Directory not found:", "Diretório não encontrado:")} {dnf.Message}");
                break;
            case UnauthorizedAccessException ua:
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Access denied:", "Acesso negado:")} {ua.Message}");
                break;
            case FbException fb:
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Firebird error:", "Erro do Firebird:")} {fb.Message}");
                Console.Error.WriteLine(TextoLocalizado.Obter(idioma,
                    "Verify connection parameters, Firebird version compatibility, and permissions.",
                    "Verifique parâmetros de conexão, compatibilidade de versão do Firebird e permissões."));
                break;
            default:
                Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Error:", "Erro:")} {ex.Message}");
                break;
        }
    }

    private static void ExibirFalhaExtracaoDdl(FalhaExtracaoDdlException falhaExtracao, IdiomaSaida idioma)
    {
        string mensagemCompleta = ObterMensagemCompleta(falhaExtracao);
        string categoria = ClassificarFalhaExtracaoDdl(mensagemCompleta);

        Console.Error.WriteLine(TextoLocalizado.Obter(idioma,
            "DDL extraction failed.",
            "A extração de DDL falhou."));
        Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Database", "Banco")}: {falhaExtracao.Database}");
        Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Failure category", "Categoria da falha")}: {categoria}");

        if (categoria == "incompatible_ods")
        {
            ExibirErroOdsIncompativel(mensagemCompleta, idioma);
            return;
        }

        if (falhaExtracao.InnerException is FbException fbInterno)
            Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Firebird error:", "Erro do Firebird:")} {fbInterno.Message}");
        else if (falhaExtracao.InnerException is not null)
            Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Error:", "Erro:")} {falhaExtracao.InnerException.Message}");
    }

    private static bool TentarExibirErroOdsIncompativel(Exception ex, IdiomaSaida idioma)
    {
        string mensagem = ObterMensagemCompleta(ex);
        if (!mensagem.Contains("unsupported on-disk structure", StringComparison.OrdinalIgnoreCase))
            return false;

        ExibirErroOdsIncompativel(mensagem, idioma);
        return true;
    }

    private static void ExibirErroOdsIncompativel(string mensagem, IdiomaSaida idioma)
    {
        string arquivo = ExtrairArquivoOds(mensagem);
        string encontrado = ExtrairGrupo(mensagem, @"found\s+(?<v>\d+(\.\d+)?)", "v");
        string suportado = ExtrairGrupo(mensagem, @"support\s+(?<v>\d+(\.\d+)?)", "v");

        Console.Error.WriteLine(TextoLocalizado.Obter(idioma,
            "Incompatible database file format (ODS).",
            "Formato de arquivo de banco incompatível (ODS)."));

        if (!string.IsNullOrWhiteSpace(arquivo))
            Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Database file", "Arquivo de banco")}: {arquivo}");
        if (!string.IsNullOrWhiteSpace(encontrado))
            Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Found ODS", "ODS encontrado")}: {encontrado}");
        if (!string.IsNullOrWhiteSpace(suportado))
            Console.Error.WriteLine($"{TextoLocalizado.Obter(idioma, "Supported ODS", "ODS suportado")}: {suportado}");

        Console.Error.WriteLine(TextoLocalizado.Obter(idioma,
            "Use a Firebird client/server compatible with this database version, or migrate the database to a supported version.",
            "Use um cliente/servidor Firebird compatível com essa versão de banco, ou migre o banco para uma versão suportada."));
    }

    private static string ClassificarFalhaExtracaoDdl(string mensagemCompleta)
    {
        string mensagem = mensagemCompleta.ToLowerInvariant();

        if (mensagem.Contains("unsupported on-disk structure"))
            return "incompatible_ods";

        if (mensagem.Contains("no permission for read-write access to database")
            || mensagem.Contains("access denied")
            || mensagem.Contains("not owner of database"))
            return "permission_denied";

        if (mensagem.Contains("i/o error for file")
            || mensagem.Contains("error while trying to open file")
            || mensagem.Contains("operating system directive open failed")
            || mensagem.Contains("cannot open file"))
            return "database_file_access";

        if (mensagem.Contains("token unknown")
            || mensagem.Contains("dynamic sql error")
            || mensagem.Contains("column unknown")
            || mensagem.Contains("table unknown"))
            return "metadata_query_failure";

        if (mensagem.Contains("unable to complete network request")
            || mensagem.Contains("connection rejected")
            || mensagem.Contains("connection shutdown"))
            return "connection_failure";

        return "unknown";
    }

    private static string ObterMensagemCompleta(Exception ex)
    {
        var partes = new List<string>();
        for (Exception? atual = ex; atual is not null; atual = atual.InnerException)
            partes.Add(atual.Message ?? string.Empty);
        return string.Join(" | ", partes);
    }

    private static string ExtrairArquivoOds(string mensagem)
    {
        var match = Regex.Match(
            mensagem,
            @"for file\s+(?<f>[^;|]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["f"].Value.Trim() : string.Empty;
    }

    private static string ExtrairGrupo(string mensagem, string padrao, string grupo)
    {
        var match = Regex.Match(
            mensagem,
            padrao,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[grupo].Value.Trim() : string.Empty;
    }

    private static string GerarPreviaComando(string comandoSql)
    {
        if (string.IsNullOrWhiteSpace(comandoSql))
            return string.Empty;

        string comandoCompactado = Regex.Replace(comandoSql, @"\s+", " ").Trim();
        const int limite = 180;

        if (comandoCompactado.Length <= limite)
            return comandoCompactado;

        return comandoCompactado.Substring(0, limite) + "...";
    }
}
