namespace SkyFBTool.Infra;

/// <summary>
/// Detecta o nome da tabela afetada por comandos SQL DML:
/// INSERT INTO, UPDATE, DELETE FROM.
///
/// Essa classe funciona de forma simples e eficiente,
/// baseada em regras bem definidas de parsing.
/// </summary>
public static class DetectarTabela
{
    /// <summary>
    /// Detecta o nome da tabela em comandos SQL do tipo:
    /// INSERT INTO Tabela ...
    /// UPDATE Tabela SET ...
    /// DELETE FROM Tabela WHERE ...
    ///
    /// Retorna NULL se o comando não for DML ou não houver tabela identificável.
    /// </summary>
    public static string? Extrair(string comandoSql)
    {
        if (string.IsNullOrWhiteSpace(comandoSql))
            return null;

        string sql = comandoSql.TrimStart().ToUpperInvariant();

        // -----------------------------------------------------------
        // 1) INSERT INTO TABELA (...)
        // -----------------------------------------------------------
        if (sql.StartsWith("INSERT INTO"))
        {
            // Exemplo: INSERT INTO Cliente (ID, Nome) VALUES (...)
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 3)
                return LimparNomeTabela(partes[2]);
        }

        // -----------------------------------------------------------
        // 2) UPDATE TABELA SET ...
        // -----------------------------------------------------------
        if (sql.StartsWith("UPDATE "))
        {
            // Exemplo: UPDATE Cliente SET Nome = 'x'
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2)
                return LimparNomeTabela(partes[1]);
        }

        // -----------------------------------------------------------
        // 3) DELETE FROM TABELA WHERE ...
        // -----------------------------------------------------------
        if (sql.StartsWith("DELETE FROM"))
        {
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 3)
                return LimparNomeTabela(partes[2]);
        }

        // -----------------------------------------------------------
        // 4) MERGE INTO TABELA USING ...
        // -----------------------------------------------------------
        if (sql.StartsWith("MERGE INTO "))
        {
            // MERGE INTO Cliente USING ...
            var partes = sql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 3)
                return LimparNomeTabela(partes[2]);
        }

        return null; // Não é DML de tabela
    }

    /// <summary>
    /// Remove:
    /// - possíveis alias (ex: Cliente C → Cliente)
    /// - caracteres especiais
    /// - parênteses opcionais
    /// </summary>
    private static string LimparNomeTabela(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return nome;

        nome = nome.Trim();

        // Remove alias após o nome da tabela
        // Ex: "CLIENTE C" → "CLIENTE"
        if (nome.Contains(' '))
            nome = nome.Split(' ')[0];

        // Remove parênteses ou vírgulas (caso raro)
        nome = nome.Trim('(', ')', ',', ';');

        return nome;
    }
}
