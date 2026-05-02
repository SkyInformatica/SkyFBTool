using SkyFBTool.Services.Ddl;

namespace SkyFBTool.Cli.Common;

public static class CliText
{
    public static string Texto(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }
}
