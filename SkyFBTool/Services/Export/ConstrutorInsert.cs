using System.Globalization;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Export;

public static class ConstrutorInsert
{
    public static string MontarInsert(
        FbDataReader leitor,
        string tabela,
        string[] colunas,
        FormatoBlob formatoBlob,
        bool forcarWin1252 = false,
        bool sanitizeTexto = false,
        bool escaparQuebras = false)
    {
        int quantidade = colunas.Length;
        string[] valores = new string[quantidade];

        for (int i = 0; i < quantidade; i++)
        {
            // Valor nulo
            if (leitor.IsDBNull(i))
            {
                valores[i] = "NULL";
                continue;
            }

            // Nome do tipo no Firebird
            string tipoNome = leitor.GetDataTypeName(i).ToUpperInvariant();

            // Tipo .NET
            Type tipo = leitor.GetFieldType(i);

            //
            // Determinar se é texto
            //
            bool ehTexto =
                   tipoNome.Contains("CHAR")
                || tipoNome.Contains("VARCHAR")
                || tipoNome.Contains("VARYING")
                || tipoNome.Contains("CSTRING")
                || tipoNome == "TEXT"
                || (tipoNome == "BLOB" && EhBlobTexto(leitor, i));

            //
            // Determinar se é binário real
            //
            bool ehBinario =
                tipo == typeof(byte[]) && !ehTexto;

            //
            // 1️⃣ RAW_WIN1252 PARA TEXTO
            //
            if (forcarWin1252 && ehTexto)
            {
                string texto = LeitorRawWin1252.LerCampoTextoComoWin1252(leitor, i);

                if (sanitizeTexto || escaparQuebras)
                    texto = SanitizadorTexto.Sanitizar(texto, escaparQuebras);

                texto = texto.Replace("'", "''");

                valores[i] = $"'{texto}'";
                continue;
            }

            //
            // 2️⃣ TEXTO NORMAL
            //
            if (ehTexto)
            {
                string texto = leitor.GetValue(i).ToString()!;

                if (sanitizeTexto || escaparQuebras)
                    texto = SanitizadorTexto.Sanitizar(texto, escaparQuebras);

                texto = texto.Replace("'", "''");

                valores[i] = $"'{texto}'";
                continue;
            }

            //
            // 3️⃣ BINÁRIO (BLOB OCTETS, BLOB SUBTYPE 0)
            //
            if (ehBinario)
            {
                byte[] dados = (byte[])leitor.GetValue(i);

                if (formatoBlob == FormatoBlob.Hex)
                    valores[i] = $"x'{ConversorHex.ParaHex(dados)}'";
                else
                    valores[i] = $"'{Convert.ToBase64String(dados)}'";

                continue;
            }

            //
            // 4️⃣ TIPOS ESPECIAIS
            //
            object valor = leitor.GetValue(i);

            // Date/DateTime → formato ISO
            if (valor is DateTime dt)
            {
                valores[i] = $"'{dt:yyyy-MM-dd HH:mm:ss}'";
                continue;
            }

            // NUMERIC / DECIMAL / FLOAT / DOUBLE → SEM NOTAÇÃO CIENTÍFICA
            if (valor is decimal dec)
            {
                valores[i] = dec.ToString("0.#########################", CultureInfo.InvariantCulture);
                continue;
            }

            if (valor is double d)
            {
                valores[i] = d.ToString("0.#########################", CultureInfo.InvariantCulture);
                continue;
            }

            if (valor is float f)
            {
                valores[i] = f.ToString("0.#########################", CultureInfo.InvariantCulture);
                continue;
            }

            // Demais: inteiro, booleano, etc
            valores[i] = valor.ToString() ?? "NULL";
        }

        return
            $"INSERT INTO {tabela} ({string.Join(", ", colunas)}) VALUES ({string.Join(", ", valores)});";
    }

    /// <summary>
    /// Detecta se um BLOB é TEXT baseado no comportamento do FirebirdClient.
    /// FirebirdClient retorna string quando sub_type = 1.
    /// </summary>
    private static bool EhBlobTexto(FbDataReader leitor, int index)
    {
        return leitor.GetFieldType(index) == typeof(string);
    }
}
