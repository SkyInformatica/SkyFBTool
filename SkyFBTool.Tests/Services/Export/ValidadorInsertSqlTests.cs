using SkyFBTool.Core;
using SkyFBTool.Services.Export;
using Xunit;

namespace SkyFBTool.Tests.Services.Export;

public class ValidadorInsertSqlTests
{
    [Fact]
    public void TentarContarColunasEValores_QuandoConsistente_RetornaTrue()
    {
        const string sql = "INSERT INTO RECIBOS (ID, NOME, OBS) VALUES (1, 'ANA', 'TEXTO');";

        var ok = ValidadorInsertSql.TentarContarColunasEValores(sql, out int colunas, out int valores, out string? erro, IdiomaSaida.PortugueseBrazil);

        Assert.True(ok);
        Assert.Equal(3, colunas);
        Assert.Equal(3, valores);
        Assert.Null(erro);
    }

    [Fact]
    public void TentarContarColunasEValores_QuandoValorTemVirgulaEmString_ContaCorretamente()
    {
        const string sql = "INSERT INTO RECIBOS (ID, OBS) VALUES (1, 'A, B, C');";

        var ok = ValidadorInsertSql.TentarContarColunasEValores(sql, out int colunas, out int valores, out string? erro, IdiomaSaida.PortugueseBrazil);

        Assert.True(ok);
        Assert.Equal(2, colunas);
        Assert.Equal(2, valores);
        Assert.Null(erro);
    }

    [Fact]
    public void TentarContarColunasEValores_QuandoInconsistente_RetornaFalseComMensagem()
    {
        const string sql = "INSERT INTO RECIBOS (ID, NOME, OBS) VALUES (1, 'ANA');";

        var ok = ValidadorInsertSql.TentarContarColunasEValores(sql, out int colunas, out int valores, out string? erro, IdiomaSaida.PortugueseBrazil);

        Assert.False(ok);
        Assert.Equal(3, colunas);
        Assert.Equal(2, valores);
        Assert.NotNull(erro);
        Assert.Contains("Quantidade de colunas", erro);
    }
}
