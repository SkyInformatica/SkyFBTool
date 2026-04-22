using System.Text;

namespace SkyFBTool.Cli.Common;

public static class CliArgumentParser
{
    public static string[] NormalizarArgs(string[] args)
    {
        if (args.Length == 0)
            return args;

        var normalizados = new List<string>(args.Length);

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (!PossuiSegmentosMesclados(arg))
            {
                normalizados.Add(arg);
                continue;
            }

            foreach (var token in SepararArgumentoMesclado(arg))
                normalizados.Add(token);
        }

        return normalizados.ToArray();
    }

    public static string LerValorOpcao(string[] args, ref int indiceAtual, string chave)
    {
        int proximoIndice = indiceAtual + 1;
        if (proximoIndice >= args.Length)
            throw new ArgumentException($"Valor não informado para --{chave}.");

        string proximoValor = args[proximoIndice].Trim().Trim('"');
        if (proximoValor.StartsWith("-"))
            throw new ArgumentException($"Valor inválido para --{chave}: {proximoValor}");

        indiceAtual = proximoIndice;
        return proximoValor;
    }

    private static bool PossuiSegmentosMesclados(string valor)
    {
        return valor.Contains(" --", StringComparison.Ordinal) ||
               valor.Contains("\t--", StringComparison.Ordinal);
    }

    private static IEnumerable<string> SepararArgumentoMesclado(string valor)
    {
        int indicePrimeiraOpcao = EncontrarInicioOpcaoMesclada(valor);
        if (indicePrimeiraOpcao < 0)
            return TokenizarArgumentoComposto(valor);

        var resultado = new List<string>();
        var prefixo = valor[..indicePrimeiraOpcao].Trim();
        if (!string.IsNullOrWhiteSpace(prefixo))
            resultado.Add(prefixo);

        var sufixo = valor[indicePrimeiraOpcao..].Trim();
        foreach (var token in TokenizarArgumentoComposto(sufixo))
            resultado.Add(token);

        return resultado;
    }

    private static int EncontrarInicioOpcaoMesclada(string valor)
    {
        for (int i = 1; i < valor.Length - 2; i++)
        {
            if (!char.IsWhiteSpace(valor[i]))
                continue;

            if (valor[i + 1] == '-' && valor[i + 2] == '-')
                return i + 1;
        }

        return -1;
    }

    private static IEnumerable<string> TokenizarArgumentoComposto(string valor)
    {
        var tokens = new List<string>();
        var atual = new StringBuilder();
        bool dentroAspas = false;

        foreach (char c in valor)
        {
            if (c == '"')
            {
                dentroAspas = !dentroAspas;
                continue;
            }

            if (!dentroAspas && char.IsWhiteSpace(c))
            {
                AdicionarToken(atual, tokens);
                continue;
            }

            atual.Append(c);
        }

        AdicionarToken(atual, tokens);
        return tokens;
    }

    private static void AdicionarToken(StringBuilder atual, List<string> tokens)
    {
        if (atual.Length == 0)
            return;

        tokens.Add(atual.ToString());
        atual.Clear();
    }
}
