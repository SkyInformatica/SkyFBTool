using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class GeradorDdlSqlTests
{
    [Fact]
    public void Gerar_QuandoExistiremObjetosBasicos_DeveManterOrdemESeparacaoPorTipo()
    {
        var snapshot = new SnapshotSchema
        {
            CharsetBanco = "UTF8",
            Dominios =
            [
                new DominioSchema { Nome = "RDB$1", TipoSql = "VARCHAR(1)" },
                new DominioSchema { Nome = "RDB$10007", TipoSql = "VARCHAR(32765)" },
                new DominioSchema { Nome = "DM_A", TipoSql = "INTEGER" },
                new DominioSchema { Nome = "DM_B", TipoSql = "INTEGER", CharsetNome = "UTF8" },
                new DominioSchema { Nome = "DM_C", TipoSql = "INTEGER", CharsetNome = "WIN1252" }
            ],
            Sequencias =
            [
                new SequenciaSchema { Nome = "SEQ_A" },
                new SequenciaSchema { Nome = "SEQ_B" }
            ],
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "DESCRICAO", TipoSql = "VARCHAR(32765)", CharsetNome = "UTF8" },
                        new ColunaSchema { Nome = "APELIDO", TipoSql = "VARCHAR(10)", CharsetNome = "WIN1252" }
                    ],
                    ChavesUnicas =
                    [
                        new ChaveUnicaSchema { Nome = "UQ_CLIENTES_ID", Colunas = ["ID"] }
                    ],
                    Indices =
                    [
                        new IndiceSchema { Nome = "IDX_CLIENTES_ID", Colunas = ["ID"] }
                    ]
                }
            ],
            Views =
            [
                new ViewSchema
                {
                    Nome = "VW_CLIENTES",
                    SelectSql = "SELECT ID FROM CLIENTES"
                }
            ],
            FuncoesExternas =
            [
                new FuncaoExternaSchema
                {
                    Nome = "UDF_EXEMPLO",
                    SourceSql = "DECLARE EXTERNAL FUNCTION \"UDF_EXEMPLO\" (CSTRING(80)) RETURNS CSTRING(80) ENTRY_POINT 'udf_exemplo' MODULE_NAME 'udf_lib';"
                }
            ]
        };

        string sql = GeradorDdlSql.Gerar(snapshot).Replace("\r\n", "\n");

        Assert.DoesNotContain("CREATE DOMAIN \"RDB$1\"", sql);
        Assert.DoesNotContain("CREATE DOMAIN \"RDB$10007\"", sql);
        int indiceDominioA = sql.IndexOf("CREATE DOMAIN \"DM_A\" AS INTEGER;", StringComparison.OrdinalIgnoreCase);
        int indiceDominioB = sql.IndexOf("CREATE DOMAIN \"DM_B\" AS INTEGER;", StringComparison.OrdinalIgnoreCase);
        int indiceDominioC = sql.IndexOf("CREATE DOMAIN \"DM_C\" AS INTEGER CHARACTER SET WIN1252;", StringComparison.OrdinalIgnoreCase);
        Assert.True(indiceDominioA >= 0);
        Assert.True(indiceDominioB > indiceDominioA);
        Assert.True(indiceDominioC > indiceDominioB);
        Assert.Contains("CREATE SEQUENCE \"SEQ_A\";\nCREATE SEQUENCE \"SEQ_B\";", sql);

        int indiceTabela = sql.IndexOf("CREATE TABLE \"CLIENTES\"", StringComparison.OrdinalIgnoreCase);
        int indiceIndice = sql.IndexOf("CREATE INDEX \"IDX_CLIENTES_ID\"", StringComparison.OrdinalIgnoreCase);
        int indiceUnique = sql.IndexOf("ALTER TABLE \"CLIENTES\" ADD CONSTRAINT \"UQ_CLIENTES_ID\" UNIQUE", StringComparison.OrdinalIgnoreCase);
        int indiceUdf = sql.IndexOf("DECLARE EXTERNAL FUNCTION \"UDF_EXEMPLO\"", StringComparison.OrdinalIgnoreCase);
        int indiceView = sql.IndexOf("CREATE VIEW \"VW_CLIENTES\"", StringComparison.OrdinalIgnoreCase);

        Assert.True(indiceTabela >= 0);
        Assert.True(indiceUnique > indiceTabela);
        Assert.True(indiceIndice > indiceUnique);
        Assert.True(indiceUdf > indiceUnique);
        Assert.True(indiceView > indiceUdf);
        Assert.Contains("\"DESCRICAO\" VARCHAR(32765)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"DESCRICAO\" VARCHAR(32765) CHARACTER SET UTF8", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"APELIDO\" VARCHAR(10) CHARACTER SET WIN1252", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gerar_QuandoColunaIncompativelComVersao_DevePreencherComentarioDeCompatibilidade()
    {
        var snapshot = new SnapshotSchema
        {
            VersaoMajor = 2,
            CharsetBanco = "WIN1252",
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ATIVO", TipoSql = "BOOLEAN", AceitaNulo = false }
                    ]
                }
            ]
        };

        string sql = GeradorDdlSql.Gerar(snapshot);

        Assert.Contains("SET NAMES WIN1252;", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-- Firebird field compatibility audit", sql);
        Assert.Contains("checked", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CAMPO_TIPO_INCOMPATIVEL_VERSAO", sql);
        Assert.Contains("CLIENTES.ATIVO", sql);
        Assert.Contains("BOOLEAN", sql);
    }

    [Fact]
    public void Gerar_QuandoFkReferenciarTabelaComPk_DeveEmitirPkAntesDaFk()
    {
        var snapshot = new SnapshotSchema
        {
            CharsetBanco = "UTF8",
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CTRLREPORTFORMSCOLUNASMODELOS",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "IDMODELO", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema { Nome = "INTEG_2249", Colunas = ["ID"] },
                    ChavesEstrangeiras =
                    [
                        new ChaveEstrangeiraSchema
                        {
                            Nome = "FK_REPORTFORMCOLUNA_2",
                            Colunas = ["IDMODELO"],
                            TabelaReferencia = "CTRLREPORTFORMSMODELOS",
                            ColunasReferencia = ["ID"],
                            RegraDelete = "RESTRICT",
                            RegraUpdate = "RESTRICT"
                        }
                    ]
                },
                new TabelaSchema
                {
                    Nome = "CTRLREPORTFORMSMODELOS",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema { Nome = "INTEG_2247", Colunas = ["ID"] }
                }
            ]
        };

        string sql = GeradorDdlSql.Gerar(snapshot);

        int indicePkReferenciada = sql.IndexOf(
            "ALTER TABLE \"CTRLREPORTFORMSMODELOS\" ADD PRIMARY KEY (\"ID\");",
            StringComparison.OrdinalIgnoreCase);
        int indiceFk = sql.IndexOf(
            "ALTER TABLE \"CTRLREPORTFORMSCOLUNASMODELOS\" ADD CONSTRAINT \"FK_REPORTFORMCOLUNA_2\" FOREIGN KEY (\"IDMODELO\") REFERENCES \"CTRLREPORTFORMSMODELOS\" (\"ID\");",
            StringComparison.OrdinalIgnoreCase);

        Assert.True(indicePkReferenciada >= 0, "PK da tabela referenciada não foi gerada.");
        Assert.True(indiceFk >= 0, "FK de referência não foi gerada.");
        Assert.True(indicePkReferenciada < indiceFk, "A PK da tabela referenciada deve ser emitida antes da FK.");
    }

    [Fact]
    public void Gerar_QuandoConstraintForInteg_DeveOmitirNomeExplicito()
    {
        var tabela = new TabelaSchema
        {
            Nome = "T1",
            Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }],
            ChavePrimaria = new ChavePrimariaSchema { Nome = "INTEG_134", Colunas = ["ID"] }
        };

        var snapshot = new SnapshotSchema
        {
            CharsetBanco = "UTF8",
            Tabelas = [tabela]
        };

        string sql = GeradorDdlSql.Gerar(snapshot);

        Assert.Contains("ALTER TABLE \"T1\" ADD PRIMARY KEY (\"ID\");", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ADD CONSTRAINT \"INTEG_134\" PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GerarCreateIndex_QuandoDescendente_DeveGerarSintaxeValidaFirebird()
    {
        var tabela = new TabelaSchema
        {
            Nome = "CAIXAS"
        };

        var indice = new IndiceSchema
        {
            Nome = "IND_CAIXAS_3",
            Descendente = true,
            Colunas = ["NOMECAIXA"]
        };

        string sql = GeradorDdlSql.GerarCreateIndex(tabela, indice);

        Assert.Equal(
            "CREATE DESCENDING INDEX \"IND_CAIXAS_3\" ON \"CAIXAS\" (\"NOMECAIXA\");",
            sql);
    }

    [Fact]
    public void GerarCreateProcedure_QuandoParametroTiverDefaultComDefaultIgual_DeveNormalizarSintaxe()
    {
        var procedimento = new ProcedimentoSchema
        {
            Nome = "ARREDONDAMENTO_ISSQN",
            ParametrosEntrada =
            [
                new ParametroProcedimentoSchema
                {
                    Nome = "TIPOARREDONDAMENTO",
                    TipoSql = "INTEGER",
                    AceitaNulo = true,
                    DefaultSql = "DEFAULT = null"
                }
            ],
            ParametrosSaida =
            [
                new ParametroProcedimentoSchema
                {
                    Nome = "ARREDONDAMENTO",
                    TipoSql = "NUMERIC(15,2)",
                    AceitaNulo = true
                }
            ],
            SourceSql = "AS BEGIN SUSPEND; END"
        };

        string sql = GeradorDdlSql.GerarCreateProcedure(procedimento);

        Assert.Contains("\"TIPOARREDONDAMENTO\" INTEGER = null", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DEFAULT = null", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gerar_QuandoProcedureDependerDeOutra_DeveOrdenarPorDependencia()
    {
        var snapshot = new SnapshotSchema
        {
            CharsetBanco = "UTF8",
            Procedimentos =
            [
                new ProcedimentoSchema
                {
                    Nome = "ARREDONDAMENTO_ISSQN",
                    SourceSql = """
                                AS
                                BEGIN
                                  SELECT ARREDONDAMENTO FROM ARREDONDAMENTO_TIPO0(:VALOR)
                                    INTO :ARRED;
                                  SUSPEND;
                                END
                                """
                },
                new ProcedimentoSchema
                {
                    Nome = "ARREDONDAMENTO_TIPO0",
                    SourceSql = """
                                AS
                                BEGIN
                                  SUSPEND;
                                END
                                """
                }
            ]
        };

        string sql = GeradorDdlSql.Gerar(snapshot);

        int indiceBase = sql.IndexOf("CREATE OR ALTER PROCEDURE \"ARREDONDAMENTO_TIPO0\"", StringComparison.OrdinalIgnoreCase);
        int indiceDependente = sql.IndexOf("CREATE OR ALTER PROCEDURE \"ARREDONDAMENTO_ISSQN\"", StringComparison.OrdinalIgnoreCase);

        Assert.True(indiceBase >= 0);
        Assert.True(indiceDependente >= 0);
        Assert.True(indiceBase < indiceDependente, "Procedure base deve ser gerada antes da dependente.");
    }

    [Fact]
    public void Gerar_QuandoProcedureUsarSelectFromProcedureSemParametros_DeveOrdenarPorDependencia()
    {
        var snapshot = new SnapshotSchema
        {
            CharsetBanco = "UTF8",
            Procedimentos =
            [
                new ProcedimentoSchema
                {
                    Nome = "LIVROCAIXAOBTERBASECALCULOLIVRO",
                    SourceSql = """
                                AS
                                DECLARE VARIABLE FESTADO VARCHAR(2);
                                BEGIN
                                  SELECT TRIM(ESTADO) FROM OBTERESTADO INTO :FESTADO;
                                  SUSPEND;
                                END
                                """
                },
                new ProcedimentoSchema
                {
                    Nome = "OBTERESTADO",
                    SourceSql = """
                                AS
                                BEGIN
                                  SUSPEND;
                                END
                                """
                }
            ]
        };

        string sql = GeradorDdlSql.Gerar(snapshot);

        int indiceBase = sql.IndexOf("CREATE OR ALTER PROCEDURE \"OBTERESTADO\"", StringComparison.OrdinalIgnoreCase);
        int indiceDependente = sql.IndexOf("CREATE OR ALTER PROCEDURE \"LIVROCAIXAOBTERBASECALCULOLIVRO\"", StringComparison.OrdinalIgnoreCase);

        Assert.True(indiceBase >= 0);
        Assert.True(indiceDependente >= 0);
        Assert.True(indiceBase < indiceDependente, "Procedure de suporte deve ser gerada antes da dependente.");
    }

    [Fact]
    public void Gerar_QuandoProcedureUsarJoinComProcedure_DeveOrdenarPorDependencia()
    {
        var snapshot = new SnapshotSchema
        {
            CharsetBanco = "UTF8",
            Procedimentos =
            [
                new ProcedimentoSchema
                {
                    Nome = "OBTERTEMPOSATENDIMENTOCAIXA",
                    SourceSql = """
                                AS
                                BEGIN
                                  FOR
                                  SELECT A.ID
                                    FROM ALGUMA_TABELA A
                                    JOIN OBTERUSUARIOLOCALATENDIMENTO(:ID) ULA ON 1=1
                                    INTO :ID
                                  DO
                                  BEGIN
                                    SUSPEND;
                                  END
                                END
                                """
                },
                new ProcedimentoSchema
                {
                    Nome = "OBTERUSUARIOLOCALATENDIMENTO",
                    SourceSql = """
                                AS
                                BEGIN
                                  SUSPEND;
                                END
                                """
                }
            ]
        };

        string sql = GeradorDdlSql.Gerar(snapshot);

        int indiceBase = sql.IndexOf("CREATE OR ALTER PROCEDURE \"OBTERUSUARIOLOCALATENDIMENTO\"", StringComparison.OrdinalIgnoreCase);
        int indiceDependente = sql.IndexOf("CREATE OR ALTER PROCEDURE \"OBTERTEMPOSATENDIMENTOCAIXA\"", StringComparison.OrdinalIgnoreCase);

        Assert.True(indiceBase >= 0);
        Assert.True(indiceDependente >= 0);
        Assert.True(indiceBase < indiceDependente, "Procedure usada em JOIN deve ser gerada antes da dependente.");
    }

    [Fact]
    public void Gerar_QuandoProcedureForChamadaComMenosParametros_DeveDefinirDefaultNullNosParametrosFinaisAnulaveis()
    {
        var snapshot = new SnapshotSchema
        {
            CharsetBanco = "UTF8",
            Procedimentos =
            [
                new ProcedimentoSchema
                {
                    Nome = "SP_LIVROCAIXAEVENTO",
                    SourceSql = """
                                AS
                                BEGIN
                                  SELECT DATAINICIAL FROM SP_LIVROCAIXASTATUS('IR') INTO :D;
                                  SUSPEND;
                                END
                                """
                },
                new ProcedimentoSchema
                {
                    Nome = "SP_LIVROCAIXASTATUS",
                    ParametrosEntrada =
                    [
                        new ParametroProcedimentoSchema { Nome = "S_TIPOLIVRO", TipoSql = "VARCHAR(10)", AceitaNulo = true },
                        new ParametroProcedimentoSchema { Nome = "S_LIVROPARCIAL", TipoSql = "VARCHAR(2)", AceitaNulo = true }
                    ],
                    SourceSql = """
                                AS
                                BEGIN
                                  SUSPEND;
                                END
                                """
                }
            ]
        };

        string sql = GeradorDdlSql.Gerar(snapshot);
        Assert.Contains("\"S_LIVROPARCIAL\" VARCHAR(2) = null", sql, StringComparison.OrdinalIgnoreCase);
    }
}
