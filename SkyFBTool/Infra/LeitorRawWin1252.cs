using System.Text;
using FirebirdSql.Data.FirebirdClient;

namespace SkyFBTool.Infra;

public static class LeitorRawWin1252
{
    private static readonly Encoding EncodingWin1252;

    static LeitorRawWin1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        EncodingWin1252 = Encoding.GetEncoding(1252);
    }

    public static string LerCampoTextoComoWin1252(FbDataReader leitor, int indice)
    {
        if (leitor.IsDBNull(indice))
            return string.Empty;

        object valor = leitor.GetValue(indice);

        switch (valor)
        {
            case byte[] raw:
                // Campo realmente binário → decodificar como Win1252
                return EncodingWin1252.GetString(raw);

            case string s:
                // Campo textual → retornar como está
                return s;

            default:
                // fallback seguro
                return valor.ToString() ?? string.Empty;
        }
    }
}