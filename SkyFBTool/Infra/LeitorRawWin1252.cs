using System.Text;
using FirebirdSql.Data.FirebirdClient;

namespace SkyFBTool.Infra;

public static class LeitorRawWin1252
{
    private static readonly Encoding EncodingWin1252 = Encoding.GetEncoding(1252);

    public static string LerCampoTextoComoWin1252(FbDataReader reader, int index)
    {
        Type tipo = reader.GetFieldType(index);

        // 🔵 CASO 1: driver devolveu string → ler via GetChars()
        if (tipo == typeof(string))
        {
            char[] buffer = new char[2048];
            long pos = 0;
            int lidos;

            using var sw = new StringWriter();

            while ((lidos = (int)reader.GetChars(index, pos, buffer, 0, buffer.Length)) > 0)
            {
                sw.Write(buffer, 0, lidos);
                pos += lidos;
            }

            // Agora temos chars em Unicode e queremos voltar para bytes Win1252
            byte[] bytes = EncodingWin1252.GetBytes(sw.ToString());

            return EncodingWin1252.GetString(bytes);
        }

        // 🔵 CASO 2: driver devolveu byte[] → ler via GetBytes()
        if (tipo == typeof(byte[]))
        {
            byte[] buffer = new byte[2048];
            long pos = 0;
            int lidos;

            using var ms = new MemoryStream();

            while ((lidos = (int)reader.GetBytes(index, pos, buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, lidos);
                pos += lidos;
            }

            byte[] dados = ms.ToArray();
            return EncodingWin1252.GetString(dados);
        }

        // Fallback seguro
        return reader.GetValue(index)?.ToString() ?? "";
    }
}