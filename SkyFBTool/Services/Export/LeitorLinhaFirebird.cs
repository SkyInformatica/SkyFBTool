using FirebirdSql.Data.FirebirdClient;

namespace SkyFBTool.Services.Export;

public static class LeitorLinhaFirebird
{
    public static object? ObterValor(FbDataReader leitor, int indice)
    {
        if (leitor.IsDBNull(indice))
            return null;

        var tipo = leitor.GetFieldType(indice);

        // BLOB binário normalmente vem como byte[]
        if (tipo == typeof(byte[]))
            return leitor.GetFieldValue<byte[]>(indice);

        // BLOB text pode vir como string dependendo do mapeamento do provider
        return leitor.GetValue(indice);
    }
}