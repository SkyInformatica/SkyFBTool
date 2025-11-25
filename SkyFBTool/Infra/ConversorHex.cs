namespace SkyFBTool.Infra;

public static class ConversorHex
{
    public static string ParaHex(byte[] dados)
    {
        const string caracteresHex = "0123456789ABCDEF";
        char[] resultado = new char[dados.Length * 2];

        for (int i = 0; i < dados.Length; i++)
        {
            int valor = dados[i];
            resultado[i * 2]     = caracteresHex[valor >> 4];
            resultado[i * 2 + 1] = caracteresHex[valor & 0x0F];
        }

        return new string(resultado);
    }
}