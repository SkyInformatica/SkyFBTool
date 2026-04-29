namespace SkyFBTool.Services.Ddl;

public class SnapshotSchema
{
    public List<TabelaSchema> Tabelas { get; set; } = [];
}

public class TabelaSchema
{
    public string Nome { get; set; } = string.Empty;
    public List<ColunaSchema> Colunas { get; set; } = [];
    public ChavePrimariaSchema? ChavePrimaria { get; set; }
    public List<ChaveEstrangeiraSchema> ChavesEstrangeiras { get; set; } = [];
    public List<IndiceSchema> Indices { get; set; } = [];
}

public class ColunaSchema
{
    public string Nome { get; set; } = string.Empty;
    public string TipoSql { get; set; } = string.Empty;
    public bool AceitaNulo { get; set; } = true;
    public string? DefaultSql { get; set; }
    public string? ComputedBySql { get; set; }
}

public class ChavePrimariaSchema
{
    public string Nome { get; set; } = string.Empty;
    public List<string> Colunas { get; set; } = [];
}

public class ChaveEstrangeiraSchema
{
    public string Nome { get; set; } = string.Empty;
    public string IndiceSuporteNome { get; set; } = string.Empty;
    public List<string> Colunas { get; set; } = [];
    public string TabelaReferencia { get; set; } = string.Empty;
    public List<string> ColunasReferencia { get; set; } = [];
    public string RegraUpdate { get; set; } = "RESTRICT";
    public string RegraDelete { get; set; } = "RESTRICT";
}

public class IndiceSchema
{
    public string Nome { get; set; } = string.Empty;
    public bool Unico { get; set; }
    public bool Descendente { get; set; }
    public List<string> Colunas { get; set; } = [];
}

public class ResultadoDiffSchema
{
    public List<string> ComandosSql { get; set; } = [];
    public List<string> ItensCriados { get; set; } = [];
    public List<string> ItensAlterados { get; set; } = [];
    public List<string> ItensSomenteNoAlvo { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
}
