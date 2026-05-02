using SkyFBTool.Services.Ddl;

namespace SkyFBTool.Cli.Common;

public static class CliBatchPatternResolver
{
    public static List<string> ResolverArquivos(
        string padraoBatch,
        IdiomaSaida idioma,
        string diretorioInexistenteEnglish,
        string diretorioInexistentePortuguese,
        string nenhumArquivoEnglish,
        string nenhumArquivoPortuguese)
    {
        string caminho = padraoBatch.Trim().Trim('"');
        string diretorio = Path.GetDirectoryName(caminho) ?? Directory.GetCurrentDirectory();
        string padrao = Path.GetFileName(caminho);

        if (string.IsNullOrWhiteSpace(padrao))
            throw new ArgumentException(CliText.Texto(idioma, "Invalid batch pattern.", "Padrão de lote inválido."));

        if (!Directory.Exists(diretorio))
            throw new DirectoryNotFoundException(CliText.Texto(idioma, diretorioInexistenteEnglish, diretorioInexistentePortuguese) + $": {diretorio}");

        var arquivos = Directory
            .GetFiles(diretorio, padrao)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (arquivos.Count == 0)
            throw new FileNotFoundException(CliText.Texto(idioma, nenhumArquivoEnglish, nenhumArquivoPortuguese) + $": {padraoBatch}");

        return arquivos;
    }

    public static bool ContemWildcard(string valor)
    {
        return valor.Contains('*') || valor.Contains('?');
    }
}
