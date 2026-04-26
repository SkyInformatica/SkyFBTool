namespace SkyFBTool.Services.Export;

public static class ValidadorInsertSql
{
    public static bool TentarContarColunasEValores(
        string insertSql,
        out int totalColunas,
        out int totalValores,
        out string? erro)
    {
        totalColunas = 0;
        totalValores = 0;
        erro = null;

        if (string.IsNullOrWhiteSpace(insertSql))
        {
            erro = "INSERT vazio.";
            return false;
        }

        int indiceAbreColunas = insertSql.IndexOf('(');
        if (indiceAbreColunas < 0)
        {
            erro = "Não foi possível localizar a lista de colunas do INSERT.";
            return false;
        }

        if (!TryFindMatchingParen(insertSql, indiceAbreColunas, out int indiceFechaColunas))
        {
            erro = "Não foi possível localizar o fechamento da lista de colunas do INSERT.";
            return false;
        }

        string listaColunas = insertSql[(indiceAbreColunas + 1)..indiceFechaColunas];
        totalColunas = ContarItensSql(listaColunas);

        if (!TryIndexOfValuesKeyword(insertSql, out int indiceValues))
        {
            erro = "Não foi possível localizar a cláusula VALUES do INSERT.";
            return false;
        }

        int indiceAbreValores = insertSql.IndexOf('(', indiceValues);
        if (indiceAbreValores < 0)
        {
            erro = "Não foi possível localizar a lista de valores do INSERT.";
            return false;
        }

        if (!TryFindMatchingParen(insertSql, indiceAbreValores, out int indiceFechaValores))
        {
            erro = "Não foi possível localizar o fechamento da lista de valores do INSERT.";
            return false;
        }

        string listaValores = insertSql[(indiceAbreValores + 1)..indiceFechaValores];
        totalValores = ContarItensSql(listaValores);

        if (totalColunas != totalValores)
        {
            erro = $"Quantidade de colunas ({totalColunas}) diferente da quantidade de valores ({totalValores}).";
            return false;
        }

        return true;
    }

    private static bool TryIndexOfValuesKeyword(string sql, out int indiceValues)
    {
        indiceValues = -1;
        const string token = "VALUES";

        bool emAspasSimples = false;
        bool emAspasDuplas = false;

        for (int i = 0; i <= sql.Length - token.Length; i++)
        {
            char atual = sql[i];

            if (atual == '\'' && !emAspasDuplas)
            {
                if (emAspasSimples && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                emAspasSimples = !emAspasSimples;
                continue;
            }

            if (atual == '"' && !emAspasSimples)
            {
                if (emAspasDuplas && i + 1 < sql.Length && sql[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                emAspasDuplas = !emAspasDuplas;
                continue;
            }

            if (emAspasSimples || emAspasDuplas)
                continue;

            if (!sql.AsSpan(i, token.Length).Equals(token, StringComparison.OrdinalIgnoreCase))
                continue;

            bool antesOk = i == 0 || !char.IsLetterOrDigit(sql[i - 1]) && sql[i - 1] != '_';
            bool depoisOk = i + token.Length >= sql.Length || !char.IsLetterOrDigit(sql[i + token.Length]) && sql[i + token.Length] != '_';

            if (antesOk && depoisOk)
            {
                indiceValues = i;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindMatchingParen(string texto, int indiceAbre, out int indiceFecha)
    {
        indiceFecha = -1;
        bool emAspasSimples = false;
        bool emAspasDuplas = false;
        int nivel = 0;

        for (int i = indiceAbre; i < texto.Length; i++)
        {
            char c = texto[i];

            if (c == '\'' && !emAspasDuplas)
            {
                if (emAspasSimples && i + 1 < texto.Length && texto[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                emAspasSimples = !emAspasSimples;
                continue;
            }

            if (c == '"' && !emAspasSimples)
            {
                if (emAspasDuplas && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                emAspasDuplas = !emAspasDuplas;
                continue;
            }

            if (emAspasSimples || emAspasDuplas)
                continue;

            if (c == '(')
            {
                nivel++;
                continue;
            }

            if (c == ')')
            {
                nivel--;
                if (nivel == 0)
                {
                    indiceFecha = i;
                    return true;
                }
            }
        }

        return false;
    }

    private static int ContarItensSql(string lista)
    {
        if (string.IsNullOrWhiteSpace(lista))
            return 0;

        bool emAspasSimples = false;
        bool emAspasDuplas = false;
        int nivelParenteses = 0;
        int total = 1;

        for (int i = 0; i < lista.Length; i++)
        {
            char c = lista[i];

            if (c == '\'' && !emAspasDuplas)
            {
                if (emAspasSimples && i + 1 < lista.Length && lista[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                emAspasSimples = !emAspasSimples;
                continue;
            }

            if (c == '"' && !emAspasSimples)
            {
                if (emAspasDuplas && i + 1 < lista.Length && lista[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                emAspasDuplas = !emAspasDuplas;
                continue;
            }

            if (emAspasSimples || emAspasDuplas)
                continue;

            if (c == '(')
            {
                nivelParenteses++;
                continue;
            }

            if (c == ')')
            {
                if (nivelParenteses > 0)
                    nivelParenteses--;
                continue;
            }

            if (c == ',' && nivelParenteses == 0)
                total++;
        }

        return total;
    }
}
