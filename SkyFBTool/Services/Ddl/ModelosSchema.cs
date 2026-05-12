namespace SkyFBTool.Services.Ddl;

public class SnapshotSchema
{
    public string? VersaoServidor { get; set; }
    public int? VersaoMajor { get; set; }
    public string? CharsetBanco { get; set; }
    public List<TabelaSchema> Tabelas { get; set; } = [];
    public List<ViewSchema> Views { get; set; } = [];
    public List<ProcedimentoSchema> Procedimentos { get; set; } = [];
    public List<FuncaoSchema> Funcoes { get; set; } = [];
    public List<FuncaoExternaSchema> FuncoesExternas { get; set; } = [];
    public List<GatilhoSchema> Gatilhos { get; set; } = [];
    public List<ExcecaoSchema> Excecoes { get; set; } = [];
    public List<DominioSchema> Dominios { get; set; } = [];
    public List<SequenciaSchema> Sequencias { get; set; } = [];
}

public class TabelaSchema
{
    public string Nome { get; set; } = string.Empty;
    public List<ColunaSchema> Colunas { get; set; } = [];
    public ChavePrimariaSchema? ChavePrimaria { get; set; }
    public List<ChaveUnicaSchema> ChavesUnicas { get; set; } = [];
    public List<RestricaoCheckSchema> RestricoesCheck { get; set; } = [];
    public List<ChaveEstrangeiraSchema> ChavesEstrangeiras { get; set; } = [];
    public List<IndiceSchema> Indices { get; set; } = [];
}

public class ColunaSchema
{
    public string Nome { get; set; } = string.Empty;
    public string TipoSql { get; set; } = string.Empty;
    public string? CharsetNome { get; set; }
    public int? BytesPorCaracter { get; set; }
    public bool AceitaNulo { get; set; } = true;
    public string? DefaultSql { get; set; }
    public string? ComputedBySql { get; set; }
}

public class ChavePrimariaSchema
{
    public string Nome { get; set; } = string.Empty;
    public List<string> Colunas { get; set; } = [];
}

public class ChaveUnicaSchema
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

public class DominioSchema
{
    public string Nome { get; set; } = string.Empty;
    public string TipoSql { get; set; } = string.Empty;
    public string? CharsetNome { get; set; }
    public int? BytesPorCaracter { get; set; }
    public bool AceitaNulo { get; set; } = true;
    public string? DefaultSql { get; set; }
    public string? CheckSql { get; set; }
}

public class SequenciaSchema
{
    public string Nome { get; set; } = string.Empty;
}

public class ViewSchema
{
    public string Nome { get; set; } = string.Empty;
    public string SelectSql { get; set; } = string.Empty;
}

public class ProcedimentoSchema
{
    public string Nome { get; set; } = string.Empty;
    public string SourceSql { get; set; } = string.Empty;
    public List<ParametroProcedimentoSchema> ParametrosEntrada { get; set; } = [];
    public List<ParametroProcedimentoSchema> ParametrosSaida { get; set; } = [];
}

public class FuncaoSchema
{
    public string Nome { get; set; } = string.Empty;
    public string SourceSql { get; set; } = string.Empty;
}

public class FuncaoExternaSchema
{
    public string Nome { get; set; } = string.Empty;
    public string SourceSql { get; set; } = string.Empty;
}

public class GatilhoSchema
{
    public string Nome { get; set; } = string.Empty;
    public string? RelacaoNome { get; set; }
    public int TipoTrigger { get; set; }
    public bool Ativo { get; set; } = true;
    public int Sequencia { get; set; }
    public string SourceSql { get; set; } = string.Empty;
}

public class ExcecaoSchema
{
    public string Nome { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
}

public class ParametroProcedimentoSchema
{
    public string Nome { get; set; } = string.Empty;
    public string TipoSql { get; set; } = string.Empty;
    public bool AceitaNulo { get; set; } = true;
    public string? DefaultSql { get; set; }
}

public class RestricaoCheckSchema
{
    public string Nome { get; set; } = string.Empty;
    public string CheckSql { get; set; } = string.Empty;
}

public class ResultadoDiffSchema
{
    public List<string> ComandosSql { get; set; } = [];
    public List<string> ItensCriados { get; set; } = [];
    public List<string> ItensAlterados { get; set; } = [];
    public List<string> ItensSomenteNoAlvo { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
}
