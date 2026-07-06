namespace SkyFBTool.Services.Ddl;

public class ResultadoAnaliseDdl
{
    public string Origem { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime GeradoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DataUltimaManutencaoUtc { get; set; }
    public string FonteDataUltimaManutencao { get; set; } = string.Empty;
    public string StatusAnaliseOperacional { get; set; } = "not_applicable";
    public string ErroAnaliseOperacional { get; set; } = string.Empty;
    public int AchadosGeradosAnaliseOperacional { get; set; }
    public ResumoObjetosAnalisadosDdl ObjetosAnalisados { get; set; } = new();
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
    public int ScoreRisco { get; set; }
    public string Prioridade { get; set; } = "P3";
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

public class ResumoObjetosAnalisadosDdl
{
    public int Tabelas { get; set; }
    public int Indices { get; set; }
    public int ChavesPrimarias { get; set; }
    public int ChavesEstrangeiras { get; set; }
    public int Triggers { get; set; }
    public int Procedures { get; set; }
    public int Functions { get; set; }
}
