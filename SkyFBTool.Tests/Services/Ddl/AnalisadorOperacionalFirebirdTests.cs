using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class AnalisadorOperacionalFirebirdTests
{
    [Fact]
    public void ClassificarFalhaOperacional_ComErroPermissao_DeveRetornarPermissionDenied()
    {
        var ex = new Exception("Dynamic SQL Error SQL error code = -551 no permission for SELECT access to MON$DATABASE");

        string classe = AnalisadorOperacionalFirebird.ClassificarFalhaOperacional(ex);

        Assert.Equal("permission_denied", classe);
    }

    [Fact]
    public void ClassificarFalhaOperacional_ComErroMetadataMon_DeveRetornarMetadataIncompatible()
    {
        var ex = new Exception("Dynamic SQL Error SQL error code = -206 Column unknown R.RDB$CARDINALITY at line 3 in MON$ query");

        string classe = AnalisadorOperacionalFirebird.ClassificarFalhaOperacional(ex);

        Assert.Equal("metadata_incompatible", classe);
    }

    [Fact]
    public void ClassificarFalhaOperacional_ComTimeout_DeveRetornarTimeout()
    {
        var ex = new TimeoutException("Operation timeout while querying MON$");

        string classe = AnalisadorOperacionalFirebird.ClassificarFalhaOperacional(ex);

        Assert.Equal("timeout", classe);
    }

    [Fact]
    public void Avaliar_ComGapOitOatCritico_DeveGerarAchadoCritico()
    {
        var metricas = new MetricasOperacionaisFirebird
        {
            OldestTransaction = 100,
            OldestActive = 250_500,
            OldestSnapshot = 250_500,
            NextTransaction = 260_000
        };

        var achados = AnalisadorOperacionalFirebird.Avaliar(metricas, IdiomaSaida.English);

        Assert.Contains(achados, a =>
            a.Codigo == "OPERACIONAL_GAP_OIT_OAT_ELEVADO" &&
            a.Severidade == "critical");
    }

    [Fact]
    public void Avaliar_ComTransacaoAtivaLonga_DeveGerarAchadoHigh()
    {
        var metricas = new MetricasOperacionaisFirebird
        {
            OldestTransaction = 1000,
            OldestActive = 2000,
            OldestSnapshot = 2100,
            NextTransaction = 2200,
            MinTimestampTransacaoAtivaUtc = DateTime.UtcNow.AddHours(-3)
        };

        var achados = AnalisadorOperacionalFirebird.Avaliar(metricas, IdiomaSaida.English);

        Assert.Contains(achados, a =>
            a.Codigo == "OPERACIONAL_TRANSACAO_ATIVA_LONGA" &&
            a.Severidade == "high");
    }

    [Fact]
    public void Avaliar_ComMetricasSaudaveis_NaoDeveGerarAchados()
    {
        var metricas = new MetricasOperacionaisFirebird
        {
            OldestTransaction = 1000,
            OldestActive = 1200,
            OldestSnapshot = 1300,
            NextTransaction = 1400
        };

        var achados = AnalisadorOperacionalFirebird.Avaliar(metricas, IdiomaSaida.English);
        Assert.Empty(achados);
    }
}
