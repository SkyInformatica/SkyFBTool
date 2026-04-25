using System.Globalization;

namespace SkyFBTool.Services.Ddl;

public enum IdiomaSaida
{
    English,
    PortugueseBrazil
}

public static class IdiomaSaidaDetector
{
    public static IdiomaSaida Detectar()
    {
        return Detectar(CultureInfo.CurrentUICulture);
    }

    public static IdiomaSaida Detectar(CultureInfo? culture)
    {
        if (culture is null)
            return IdiomaSaida.English;

        return culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? IdiomaSaida.PortugueseBrazil
            : IdiomaSaida.English;
    }
}
