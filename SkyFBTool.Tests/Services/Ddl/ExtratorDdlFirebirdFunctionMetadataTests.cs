using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class ExtratorDdlFirebirdFunctionMetadataTests
{
    [Fact]
    public void EhFuncaoPsqlArmazenada_QuandoFuncaoForTopLevel_DeveRetornarTrue()
    {
        bool resultado = ExtratorDdlFirebird.EhFuncaoPsqlArmazenada(
            moduleName: null,
            entrypoint: null,
            legacyFlag: 0,
            engineName: null,
            packageName: null,
            privateFlag: null);

        Assert.True(resultado);
    }

    [Theory]
    [InlineData("udf_lib", "udf_entry", 1, null, null, null)]
    [InlineData(null, null, 0, "UDR", null, null)]
    [InlineData(null, null, 0, null, "PKG_UTIL", 0)]
    [InlineData(null, null, 0, null, "PKG_UTIL", 1)]
    public void EhFuncaoPsqlArmazenada_QuandoFuncaoForExternaOuPackage_DeveRetornarFalse(
        string? moduleName,
        string? entrypoint,
        int? legacyFlag,
        string? engineName,
        string? packageName,
        int? privateFlag)
    {
        bool resultado = ExtratorDdlFirebird.EhFuncaoPsqlArmazenada(
            moduleName,
            entrypoint,
            legacyFlag,
            engineName,
            packageName,
            privateFlag);

        Assert.False(resultado);
    }
}
