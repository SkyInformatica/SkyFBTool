using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Services.Ddl;

namespace SkyFBTool.Cli.Common;

public static class CliErrorHandler
{
    public static void Exibir(Exception ex)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(M(idioma, "Execution failed.", "Execução falhou."));
        Console.ResetColor();

        if (TentarExibirErroOdsIncompativel(ex, idioma))
            return;

        switch (ex)
        {
            case ArgumentException arg:
                Console.Error.WriteLine($"{M(idioma, "Invalid arguments:", "Argumentos inválidos:")} {arg.Message}");
                break;
            case FileNotFoundException fnf:
                Console.Error.WriteLine($"{M(idioma, "File not found:", "Arquivo não encontrado:")} {fnf.Message}");
                break;
            case DirectoryNotFoundException dnf:
                Console.Error.WriteLine($"{M(idioma, "Directory not found:", "Diretório não encontrado:")} {dnf.Message}");
                break;
            case UnauthorizedAccessException ua:
                Console.Error.WriteLine($"{M(idioma, "Access denied:", "Acesso negado:")} {ua.Message}");
                break;
            case FbException fb:
                Console.Error.WriteLine($"{M(idioma, "Firebird error:", "Erro do Firebird:")} {fb.Message}");
                Console.Error.WriteLine(M(
                    idioma,
                    "Verify connection parameters, Firebird version compatibility, and permissions.",
                    "Verifique parâmetros de conexão, compatibilidade de versão do Firebird e permissões."));
                break;
            default:
                Console.Error.WriteLine($"{M(idioma, "Error:", "Erro:")} {ex.Message}");
                break;
        }
    }

    private static bool TentarExibirErroOdsIncompativel(Exception ex, IdiomaSaida idioma)
    {
        string mensagem = ObterMensagemCompleta(ex);
        if (!mensagem.Contains("unsupported on-disk structure", StringComparison.OrdinalIgnoreCase))
            return false;

        string arquivo = ExtrairArquivoOds(mensagem);
        string encontrado = ExtrairGrupo(mensagem, @"found\s+(?<v>\d+(\.\d+)?)", "v");
        string suportado = ExtrairGrupo(mensagem, @"support\s+(?<v>\d+(\.\d+)?)", "v");

        Console.Error.WriteLine(M(
            idioma,
            "Incompatible database file format (ODS).",
            "Formato de arquivo de banco incompatível (ODS)."));

        if (!string.IsNullOrWhiteSpace(arquivo))
            Console.Error.WriteLine($"{M(idioma, "Database file", "Arquivo de banco")}: {arquivo}");
        if (!string.IsNullOrWhiteSpace(encontrado))
            Console.Error.WriteLine($"{M(idioma, "Found ODS", "ODS encontrado")}: {encontrado}");
        if (!string.IsNullOrWhiteSpace(suportado))
            Console.Error.WriteLine($"{M(idioma, "Supported ODS", "ODS suportado")}: {suportado}");

        Console.Error.WriteLine(M(
            idioma,
            "Use a Firebird client/server compatible with this database version, or migrate the database to a supported version.",
            "Use um cliente/servidor Firebird compatível com essa versão de banco, ou migre o banco para uma versão suportada."));

        return true;
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

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}
