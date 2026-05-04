namespace SkyFBTool.Services.Ddl;

public class ResultadoAuditoriaCompatibilidadeCamposDdl
{
    public string? VersaoServidor { get; set; }
    public int? VersaoMajor { get; set; }
    public int TotalItens { get; set; }
    public int TotalOk { get; set; }
    public int TotalProblemas { get; set; }
    public List<ItemAuditoriaCompatibilidadeCampoDdl> Itens { get; set; } = [];
}

public class ItemAuditoriaCompatibilidadeCampoDdl
{
    public string TipoObjeto { get; set; } = string.Empty;
    public string Escopo { get; set; } = string.Empty;
    public string TipoSql { get; set; } = string.Empty;
    public string? CharsetNome { get; set; }
    public int? BytesPorCaracter { get; set; }
    public int? TamanhoDeclarado { get; set; }
    public int? TamanhoEfetivoBytes { get; set; }
    public string Status { get; set; } = "ok";
    public string Codigo { get; set; } = "OK";
    public string Mensagem { get; set; } = string.Empty;
    public string Recomendacao { get; set; } = string.Empty;
    public int? VersaoMinimaMajor { get; set; }
    public int? Limite { get; set; }
    public int? Valor { get; set; }
}

public class AchadoCompatibilidadeCampoDdl
{
    public string Severidade { get; set; } = "medium";
    public string Codigo { get; set; } = string.Empty;
    public string Escopo { get; set; } = string.Empty;
    public string TipoSql { get; set; } = string.Empty;
    public string? CharsetNome { get; set; }
    public int? BytesPorCaracter { get; set; }
    public int? TamanhoDeclarado { get; set; }
    public int? TamanhoEfetivoBytes { get; set; }
    public int? VersaoMinimaMajor { get; set; }
    public int? Limite { get; set; }
    public int? Valor { get; set; }
}
