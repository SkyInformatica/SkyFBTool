namespace SkyFBTool.Services.Export;

public static class ConstrutorConsultaFirebird
{
    public static string MontarSelect(string nomeTabela, string? condicaoWhere)
    {
        if (string.IsNullOrWhiteSpace(condicaoWhere))
            return $"SELECT * FROM {nomeTabela}";

        return $"SELECT * FROM {nomeTabela} WHERE {condicaoWhere}";
    }
}