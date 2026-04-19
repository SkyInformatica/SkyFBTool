namespace SkyFBTool.Infra;

public static class DetectarTabela
{
    public static string? Extrair(string comandoSql)
    {
        if (string.IsNullOrWhiteSpace(comandoSql))
            return null;

        string sql = comandoSql.TrimStart().ToUpperInvariant();

        if (sql.StartsWith("INSERT INTO"))
        {
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 3)
                return LimparNomeTabela(partes[2]);
        }

        if (sql.StartsWith("UPDATE "))
        {
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2)
                return LimparNomeTabela(partes[1]);
        }

        if (sql.StartsWith("DELETE FROM"))
        {
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 3)
                return LimparNomeTabela(partes[2]);
        }

        if (sql.StartsWith("MERGE INTO "))
        {
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 3)
                return LimparNomeTabela(partes[2]);
        }

        return null;
    }

    private static string LimparNomeTabela(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return nome;

        nome = nome.Trim();

        if (nome.Contains(' '))
            nome = nome.Split(' ')[0];

        nome = nome.Trim('(', ')', ',', ';');

        return nome;
    }
}
