using System.Globalization;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class IdiomaSaidaDetectorTests
{
    [Fact]
    public void Detectar_ComCulturaEnUs_DeveRetornarEnglish()
    {
        var idioma = IdiomaSaidaDetector.Detectar(new CultureInfo("en-US"));
        Assert.Equal(IdiomaSaida.English, idioma);
    }

    [Fact]
    public void Detectar_ComCulturaPtBr_DeveRetornarPortugueseBrazil()
    {
        var idioma = IdiomaSaidaDetector.Detectar(new CultureInfo("pt-BR"));
        Assert.Equal(IdiomaSaida.PortugueseBrazil, idioma);
    }
}
