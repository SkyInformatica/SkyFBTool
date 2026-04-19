using System.Text;

namespace SkyFBTool.Infra;

public static class CharsetSql
{
    static CharsetSql()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string DetectarCharsetSetNames(string caminhoArquivoSql, int limiteLinhas = 200)
    {
        using var leitor = new StreamReader(
            caminhoArquivoSql,
            new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: true);

        string? linha;
        int limite = limiteLinhas;

        while (limite-- > 0 && (linha = leitor.ReadLine()) != null)
        {
            string l = linha.Trim();

            if (!l.StartsWith("SET NAMES", StringComparison.OrdinalIgnoreCase))
                continue;

            var tokens = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
                continue;

            return tokens[2].Replace(";", "").Trim().ToUpperInvariant();
        }

        return "UTF8";
    }

    public static Encoding ResolverEncodingLeituraSql(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
            return new UTF8Encoding(false);

        string nome = charset.Trim().ToUpperInvariant();

        return nome switch
        {
            "WIN1252" => Encoding.GetEncoding(1252),
            "ISO8859_1" => Encoding.GetEncoding("iso-8859-1"),
            "ISO-8859-1" => Encoding.GetEncoding("iso-8859-1"),
            "UTF8" => new UTF8Encoding(false),
            "UTF-8" => new UTF8Encoding(false),
            _ => TentarResolverOuUtf8(nome)
        };
    }

    private static Encoding TentarResolverOuUtf8(string nome)
    {
        try
        {
            return Encoding.GetEncoding(nome);
        }
        catch
        {
            return new UTF8Encoding(false);
        }
    }
}
