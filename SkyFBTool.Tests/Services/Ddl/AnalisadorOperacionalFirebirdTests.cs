using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class AnalisadorOperacionalFirebirdTests
{
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
