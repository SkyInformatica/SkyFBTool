using FirebirdSql.Data.FirebirdClient;

namespace SkyFBTool.Services.Export;

public static class LeitorLinhaFirebird
{
    public static object? ObterValor(FbDataReader leitor, int indice)
    {
        if (leitor.IsDBNull(indice))
            return null;

        var tipo = leitor.GetFieldType(indice);

        if (tipo == typeof(byte[]))
            return leitor.GetFieldValue<byte[]>(indice);

        return leitor.GetValue(indice);
    }
}
