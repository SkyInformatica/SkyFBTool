using System.Text;
using SkyFBTool.Infra;
using Xunit;

namespace SkyFBTool.Tests.Infra;

public class CharsetSqlTests
{
    [Fact]
    public void DetectarCharsetSetNames_QuandoNaoExisteSetNames_RetornaUtf8()
    {
        string arquivo = CriarArquivoTemporario(
            "SELECT 1 FROM RDB$DATABASE;",
            new UTF8Encoding(false));

        try
        {
            string charset = CharsetSql.DetectarCharsetSetNames(arquivo);
            Assert.Equal("UTF8", charset);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public void DetectarCharsetSetNames_QuandoExisteSetNames_RetornaValorNormalizado()
    {
        string arquivo = CriarArquivoTemporario(
            "  set names win1252;\r\nINSERT INTO T (N) VALUES ('A');",
            new UTF8Encoding(false));

        try
        {
            string charset = CharsetSql.DetectarCharsetSetNames(arquivo);
            Assert.Equal("WIN1252", charset);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Theory]
    [InlineData("WIN1252", 1252)]
    [InlineData("ISO8859_1", 28591)]
    [InlineData("UTF8", 65001)]
    public void ResolverEncodingLeituraSql_RetornaEncodingEsperado(string charset, int codePageEsperada)
    {
        var encoding = CharsetSql.ResolverEncodingLeituraSql(charset);
        Assert.Equal(codePageEsperada, encoding.CodePage);
    }

    [Fact]
    public void ResolverEncodingLeituraSql_Win1252_LeTextoComAcentoCorretamente()
    {
        string conteudo = "SET NAMES WIN1252;\r\nINSERT INTO T (NOME) VALUES ('ação');";
        var win1252 = Encoding.GetEncoding(1252);
        string arquivo = CriarArquivoTemporario(conteudo, win1252);

        try
        {
            string charset = CharsetSql.DetectarCharsetSetNames(arquivo);
            var encoding = CharsetSql.ResolverEncodingLeituraSql(charset);

            string textoLido = File.ReadAllText(arquivo, encoding);
            Assert.Contains("ação", textoLido);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    private static string CriarArquivoTemporario(string conteudo, Encoding encoding)
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"skyfbtool_charset_{Guid.NewGuid():N}.sql");
        File.WriteAllText(caminho, conteudo, encoding);
        return caminho;
    }
}
