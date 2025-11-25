using System.Data;
using System.Globalization;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Export;

public static class ConstrutorInsert
{
    private static readonly CultureInfo CulturaInvariante = CultureInfo.InvariantCulture;

    public static string MontarInsert(
        FbDataReader leitor,
        string tabelaDestino,
        string[] colunas,
        FormatoBlob formatoBlob,
        bool forcarWin1252,
        bool sanitizarTexto,
        bool escaparQuebrasDeLinha)
    {
        var sb = new StringBuilder();

        sb.Append("INSERT INTO ")
          .Append(tabelaDestino)
          .Append(" (")
          .Append(string.Join(", ", colunas))
          .Append(") VALUES (");

        for (int i = 0; i < colunas.Length; i++)
        {
            if (i > 0)
                sb.Append(", ");

            if (leitor.IsDBNull(i))
            {
                sb.Append("NULL");
                continue;
            }

            var tipo = leitor.GetDataTypeName(i).ToUpperInvariant();

            switch (tipo)
            {
                case "SMALLINT":
                case "INTEGER":
                case "BIGINT":
                    sb.Append(leitor.GetValue(i).ToString());
                    break;

                case "NUMERIC":
                case "DECIMAL":
                    var valorDecimal = Convert.ToDecimal(leitor.GetValue(i), CulturaInvariante);
                    sb.Append(valorDecimal.ToString(CulturaInvariante));
                    break;

                case "DATE":
                    var data = leitor.GetDateTime(i);
                    sb.Append('\'')
                      .Append(data.ToString("yyyy-MM-dd", CulturaInvariante))
                      .Append('\'');
                    break;

                case "TIMESTAMP":
                    var dataHora = leitor.GetDateTime(i);
                    sb.Append('\'')
                      .Append(dataHora.ToString("yyyy-MM-dd HH:mm:ss", CulturaInvariante))
                      .Append('\'');
                    break;

                case "BLOB":
                    sb.Append(FormatarBlob(leitor, i, formatoBlob));
                    break;

                case "CHAR":
                case "VARCHAR":
                default:
                    string texto;

                    if (forcarWin1252)
                        texto = LeitorRawWin1252.LerCampoTextoComoWin1252(leitor, i);
                    else
                        texto = leitor.GetString(i);

                    if (sanitizarTexto)
                        texto = SanitizadorTexto.Sanitizar(texto);

                    if (escaparQuebrasDeLinha)
                    {
                        texto = texto.Replace("\r", "\\r")
                                     .Replace("\n", "\\n");
                    }

                    texto = texto.Replace("'", "''");

                    sb.Append('\'')
                      .Append(texto)
                      .Append('\'');
                    break;
            }
        }

        sb.Append(");");
        return sb.ToString();
    }

    private static string FormatarBlob(FbDataReader leitor, int indice, FormatoBlob formatoBlob)
    {
        var dados = leitor.GetFieldValue<byte[]>(indice);

        return formatoBlob switch
        {
            FormatoBlob.Hex => "x'" + BitConverter.ToString(dados).Replace("-", "") + "'",
            FormatoBlob.Base64 => "'" + Convert.ToBase64String(dados) + "'",
            _ => "NULL"
        };
    }
}
