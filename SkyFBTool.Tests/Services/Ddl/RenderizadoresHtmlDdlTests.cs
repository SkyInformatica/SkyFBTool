using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class RenderizadoresHtmlDdlTests
{
    [Fact]
    public void RenderizarAnalise_DeveExporVersaoDoRelatorio()
    {
        var resultado = new ResultadoAnaliseDdl
        {
            Origem = "origem.schema.json"
        };

        string html = RenderizadorHtmlAnaliseDdl.Renderizar(resultado, IdiomaSaida.English);

        Assert.Contains("Report version", html);
        Assert.Contains(RenderizadorHtmlAnaliseDdl.VersaoRelatorio, html);
        Assert.Contains(
            $"<meta name=\"skyfbtool-report-version\" content=\"{RenderizadorHtmlAnaliseDdl.VersaoRelatorio}\" />",
            html);
        Assert.Contains("Generated at (local)", html);
        Assert.DoesNotContain(TimeZoneInfo.Local.Id, html);
    }

    [Fact]
    public void RenderizarDiff_DeveExporVersaoDoRelatorio()
    {
        var resultado = new ResultadoDiffSchema();

        string html = RenderizadorHtmlDiffDdl.Renderizar(
            resultado,
            "origem.schema.json",
            "alvo.schema.json",
            IdiomaSaida.English);

        Assert.Contains("Report version", html);
        Assert.Contains(RenderizadorHtmlDiffDdl.VersaoRelatorio, html);
        Assert.Contains(
            $"<meta name=\"skyfbtool-report-version\" content=\"{RenderizadorHtmlDiffDdl.VersaoRelatorio}\" />",
            html);
        Assert.Contains("Generated at (local)", html);
        Assert.DoesNotContain(TimeZoneInfo.Local.Id, html);
    }
}
