using System.Text;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Export;

public static class ConstrutorConsultaFirebird
{
    public static string MontarSelect(OpcoesExportacao opcoes)
    {
        var nomeTabela = opcoes.Tabela;

        var sb = new StringBuilder();
        sb.Append("SELECT * FROM ").Append(nomeTabela);

        if (!string.IsNullOrWhiteSpace(opcoes.CondicaoWhere))
        {
            sb.Append(" WHERE ").Append(opcoes.CondicaoWhere);
        }

        return sb.ToString();
    }
}