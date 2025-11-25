using System.Text;

namespace SkyFBTool.Infra;

public static class SanitizadorTexto
{
    public static string Sanitizar(string texto, bool escaparNewLines)
    {
        if (string.IsNullOrEmpty(texto))
            return texto;

        var sb = new StringBuilder(texto.Length);

        foreach (char c in texto)
        {
            // manter caracteres válidos
            if (c == '\t' || c == '\n' || c == '\r')
            {
                // opcionalmente transformar CRLF → \n
                if (escaparNewLines)
                {
                    if (c == '\n') sb.Append("\\n");
                    else if (c == '\r') sb.Append("\\r");
                    else sb.Append(c);
                }
                else
                {
                    sb.Append(c);
                }

                continue;
            }

            // remover control chars 0x00–0x1F, exceto CR/LF/TAB
            if (char.IsControl(c))
                continue;

            // NBSP → espaço
            if (c == '\u00A0')
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}