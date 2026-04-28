namespace SkyFBTool.Services.Ddl;

public class ResultadoAnaliseDdl
{
    public string Origem { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime GeradoEmUtc { get; set; } = DateTime.UtcNow;
    public int TotalTabelas { get; set; }
    public int TotalAchados { get; set; }
    public int TotalCriticos { get; set; }
    public int TotalAltos { get; set; }
    public int TotalMedios { get; set; }
    public int TotalBaixos { get; set; }
    public List<AchadoAnaliseDdl> Achados { get; set; } = [];
    public List<ItemResumoAnaliseDdl> ResumoPorCodigo { get; set; } = [];
    public List<ItemResumoAnaliseDdl> ResumoPorTabela { get; set; } = [];
}

public class AchadoAnaliseDdl
{
    public string Severidade { get; set; } = "low";
    public string Codigo { get; set; } = string.Empty;
    public string Escopo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Recomendacao { get; set; } = string.Empty;
}

public class ItemResumoAnaliseDdl
{
    public string Chave { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal Percentual { get; set; }
}
