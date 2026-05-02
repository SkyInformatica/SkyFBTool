namespace SkyFBTool.Core;

public static class TextoLocalizado
{
    public static string Obter(IdiomaSaida idioma, string english, string portuguese)
        => idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
}
