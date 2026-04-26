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
        (int Ordinal, string Nome)[] colunas,
        ModoInsertExportacao modoInsert,
        IReadOnlyList<string>? colunasMatching,
        FormatoBlob formatoBlob,
        bool forcarWin1252,
        bool sanitizarTexto,
        bool escaparQuebrasDeLinha)
    {
        var sb = new StringBuilder();

        string comando = modoInsert == ModoInsertExportacao.Upsert
            ? "UPDATE OR INSERT INTO "
            : "INSERT INTO ";

        sb.Append(comando)
          .Append(tabelaDestino)
          .Append(" (")
          .Append(string.Join(", ", colunas.Select(c => c.Nome)))
          .Append(") VALUES (");

        for (int i = 0; i < colunas.Length; i++)
        {
            int indiceLeitor = colunas[i].Ordinal;

            if (i > 0)
                sb.Append(", ");

            if (leitor.IsDBNull(indiceLeitor))
            {
                sb.Append("NULL");
                continue;
            }

            var tipo = leitor.GetDataTypeName(indiceLeitor).ToUpperInvariant();

            switch (tipo)
            {
                case "SMALLINT":
                case "INTEGER":
                case "BIGINT":
                    sb.Append(leitor.GetValue(indiceLeitor).ToString());
                    break;

                case "NUMERIC":
                case "DECIMAL":
                    var valorDecimal = Convert.ToDecimal(leitor.GetValue(indiceLeitor), CulturaInvariante);
                    sb.Append(valorDecimal.ToString(CulturaInvariante));
                    break;

                case "DATE":
                    var data = leitor.GetDateTime(indiceLeitor);
                    sb.Append('\'')
                      .Append(data.ToString("yyyy-MM-dd", CulturaInvariante))
                      .Append('\'');
                    break;

                case "TIMESTAMP":
                    var dataHora = leitor.GetDateTime(indiceLeitor);
                    sb.Append('\'')
                      .Append(dataHora.ToString("yyyy-MM-dd HH:mm:ss", CulturaInvariante))
                      .Append('\'');
                    break;

                case "BLOB":
                    var valor = leitor.GetValue(indiceLeitor);

                    if (valor is byte[] bytesBlob)
                    {
                        sb.Append(FormatarBlobBinario(bytesBlob, formatoBlob));
                    }
                    else if (valor is string textoBlob)
                    {
                        textoBlob = textoBlob.Replace("'", "''");
                        sb.Append('\'').Append(textoBlob).Append('\'');
                    }
                    else
                    {
                        sb.Append("NULL");
                    }
                    break;


                case "CHAR":
                case "VARCHAR":
                default:
                    string texto;

                    if (forcarWin1252)
                        texto = LeitorRawWin1252.LerCampoTextoComoWin1252(leitor, indiceLeitor);
                    else
                        texto = leitor.GetString(indiceLeitor);

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

        sb.Append(')');

        if (modoInsert == ModoInsertExportacao.Upsert)
        {
            if (colunasMatching is null || colunasMatching.Count == 0)
                throw new InvalidOperationException("Modo upsert exige colunas MATCHING.");

            sb.Append(" MATCHING (")
              .Append(string.Join(", ", colunasMatching))
              .Append(')');
        }

        sb.Append(';');
        var insertSql = sb.ToString();

        if (!ValidadorInsertSql.TentarContarColunasEValores(insertSql, out int totalColunas, out int totalValores, out string? erro))
        {
            throw new InvalidOperationException(
                $"Falha de consistência ao gerar INSERT para '{tabelaDestino}'. {erro}");
        }

        if (totalColunas != totalValores)
        {
            throw new InvalidOperationException(
                $"Falha de consistência ao gerar INSERT para '{tabelaDestino}': {totalColunas} colunas e {totalValores} valores.");
        }

        return insertSql;
    }

    private static string FormatarBlobBinario(byte[] dados, FormatoBlob formatoBlob)
    {
        return formatoBlob switch
        {
            FormatoBlob.Hex => "x'" + BitConverter.ToString(dados).Replace("-", "") + "'",
            FormatoBlob.Base64 => "'" + Convert.ToBase64String(dados) + "'",
            _ => "NULL"
        };
    }

}
