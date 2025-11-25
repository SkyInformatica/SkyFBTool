namespace SkyFBTool.Infra;

public static class SanitizadorTexto
{
    public static string Sanitizar(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return texto;

        Span<char> buffer = stackalloc char[texto.Length];
        int pos = 0;

        foreach (var c in texto)
        {
            // Mantém basicamente ASCII + acentuação latina
            if (c == '\r' || c == '\n' || c == '\t' ||
                (c >= ' ' && c <= '\u00FF'))
            {
                buffer[pos++] = c;
            }
        }

        return new string(buffer[..pos]);
    }
}