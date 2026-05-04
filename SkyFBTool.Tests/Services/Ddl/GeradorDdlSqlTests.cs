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
        Assert.True(indiceIndice > indiceTabela);
        Assert.True(indiceUnique > indiceIndice);
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
}
