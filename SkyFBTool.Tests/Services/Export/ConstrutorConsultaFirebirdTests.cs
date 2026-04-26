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
    public void MontarSelectComColunas_ComDadosValidos_GeraSqlEsperado()
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "RECIBOS",
            CondicaoWhere = "NUMERORECIBO = 1"
        };

        var sql = ConstrutorConsultaFirebird.MontarSelectComColunas(opcoes, new[] { "NUMERORECIBO", "TALAORECIBO" });

        Assert.Equal("SELECT \"NUMERORECIBO\", \"TALAORECIBO\" FROM RECIBOS WHERE NUMERORECIBO = 1", sql);
    }

    [Fact]
    public void MontarSelectComColunas_SemColunas_DisparaErro()
    {
        var opcoes = new OpcoesExportacao { Tabela = "RECIBOS" };

        var ex = Assert.Throws<ArgumentException>(() => ConstrutorConsultaFirebird.MontarSelectComColunas(opcoes, Array.Empty<string>()));

        Assert.Contains("Nenhuma coluna válida", ex.Message);
    }

    [Fact]
    public void MontarSelect_ComQueryCompleta_UsaSelectDoArquivo()
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "PAGAMENTOS",
            ConsultaSqlCompleta = "SELECT r.* FROM RECIBOS r WHERE r.NUMERORECIBO = 1;"
        };

        var sql = ConstrutorConsultaFirebird.MontarSelect(opcoes);

        Assert.Equal("SELECT r.* FROM RECIBOS r WHERE r.NUMERORECIBO = 1", sql);
    }

    [Fact]
    public void MontarSelect_ComQueryCompletaNaoSelect_DisparaErro()
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "PAGAMENTOS",
            ConsultaSqlCompleta = "DELETE FROM RECIBOS"
        };

        var ex = Assert.Throws<ArgumentException>(() => ConstrutorConsultaFirebird.MontarSelect(opcoes));

        Assert.Contains("Consulta SQL inválida", ex.Message);
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

    [Fact]
    public void MontarSelect_WhereComPrefixoWhereEQuebraLinha_RemovePrefixo()
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "PAGAMENTOS",
            CondicaoWhere = "WHERE\r\nCODIGO = 10"
        };

        var sql = ConstrutorConsultaFirebird.MontarSelect(opcoes);

        Assert.Equal("SELECT * FROM PAGAMENTOS WHERE CODIGO = 10", sql);
    }

    [Fact]
    public void MontarSelect_WhereSomenteComPalavraWhere_DisparaErro()
    {
        var opcoes = new OpcoesExportacao
        {
            Tabela = "PAGAMENTOS",
            CondicaoWhere = "WHERE"
        };

        var ex = Assert.Throws<ArgumentException>(() => ConstrutorConsultaFirebird.MontarSelect(opcoes));

        Assert.Contains("Condição WHERE inválida", ex.Message);
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
