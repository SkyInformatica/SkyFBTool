using SkyFBTool.Core;
using SkyFBTool.Services.Export;
using Xunit;

namespace SkyFBTool.Tests.Services.Export;

public class ConstrutorConsultaFirebirdTests
{
    [Fact]
    public void MontarSelect_ComDadosValidos_GeraSqlEsperado()
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "PAGAMENTOS",
            CondicaoWhere = "CODIGO = 10"
        };

        var sql = ConstrutorConsultaFirebird.MontarSelect(opcoes);

        Assert.Equal("SELECT * FROM PAGAMENTOS WHERE CODIGO = 10", sql);
    }

    [Fact]
    public void MontarSelect_WhereComPrefixoWhere_RemovePrefixo()
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "PAGAMENTOS",
            CondicaoWhere = "WHERE CODIGO = 10"
        };

        var sql = ConstrutorConsultaFirebird.MontarSelect(opcoes);

        Assert.Equal("SELECT * FROM PAGAMENTOS WHERE CODIGO = 10", sql);
    }

    [Theory]
    [InlineData("PAGAMENTOS; DROP TABLE X")]
    [InlineData("PAGAMENTOS --teste")]
    [InlineData("PAGAMENTOS/*x*/")]
    public void MontarSelect_TabelaInvalida_DisparaErro(string tabela)
    {
        var opcoes = new OpcoesExportacao { Tabela = tabela };

        var ex = Assert.Throws<ArgumentException>(() => ConstrutorConsultaFirebird.MontarSelect(opcoes));

        Assert.Contains("Nome de tabela inválido", ex.Message);
    }

    [Theory]
    [InlineData("ID = 1; DELETE FROM A")]
    [InlineData("ID = 1 -- comentário")]
    [InlineData("ID = 1 /* comentário */")]
    public void MontarSelect_WhereInvalido_DisparaErro(string where)
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "PAGAMENTOS",
            CondicaoWhere = where
        };

        var ex = Assert.Throws<ArgumentException>(() => ConstrutorConsultaFirebird.MontarSelect(opcoes));

        Assert.Contains("Condição WHERE inválida", ex.Message);
    }
}
