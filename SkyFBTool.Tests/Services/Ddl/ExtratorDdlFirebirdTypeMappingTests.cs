using System.Reflection;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class ExtratorDdlFirebirdTypeMappingTests
{
    [Theory]
    [InlineData(27, 0, 8, null, 0, null, "DOUBLE PRECISION")]
    [InlineData(28, 0, 0, null, 0, null, "TIME WITH TIME ZONE")]
    [InlineData(29, 0, 0, null, 0, null, "TIMESTAMP WITH TIME ZONE")]
    public void MapearTipoSql_DeveMapearTiposModernosCorretamente(
        int fieldType,
        int fieldSubtype,
        int fieldLength,
        int? precision,
        int scale,
        int? characterLength,
        string esperado)
    {
        MethodInfo? metodo = typeof(ExtratorDdlFirebird).GetMethod(
            "MapearTipoSql",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(metodo);

        object? retorno = metodo!.Invoke(
            null,
            [fieldType, fieldSubtype, fieldLength, precision, scale, characterLength]);

        Assert.Equal(esperado, retorno as string);
    }
}

