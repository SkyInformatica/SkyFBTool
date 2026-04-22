using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class ComparadorSchemaTests
{
    [Fact]
    public void GerarDiff_QuandoTabelaNaoExisteNoAlvo_DeveGerarCreateTable()
    {
        var origem = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }
                    ]
                }
            ]
        };

        var alvo = new SnapshotSchema();

        var diff = ComparadorSchema.GerarDiff(origem, alvo);

        Assert.Contains(diff.ItensCriados, i => i.Contains("CLIENTES", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diff.ComandosSql, sql => sql.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GerarDiff_QuandoColunaNaoExisteNoAlvo_DeveGerarAlterTableAdd()
    {
        var origem = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "NOME", TipoSql = "VARCHAR(120)", AceitaNulo = true }
                    ]
                }
            ]
        };

        var alvo = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }
                    ]
                }
            ]
        };

        var diff = ComparadorSchema.GerarDiff(origem, alvo);

        Assert.Contains(diff.ComandosSql, sql =>
            sql.Contains("ALTER TABLE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("ADD", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("NOME", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GerarDiff_QuandoTipoOuDefaultMudam_DeveGerarAlteracoes()
    {
        var origem = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "PEDIDOS",
                    Colunas =
                    [
                        new ColunaSchema
                        {
                            Nome = "VALOR_TOTAL",
                            TipoSql = "NUMERIC(15,2)",
                            AceitaNulo = false,
                            DefaultSql = "DEFAULT 0"
                        }
                    ]
                }
            ]
        };

        var alvo = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "PEDIDOS",
                    Colunas =
                    [
                        new ColunaSchema
                        {
                            Nome = "VALOR_TOTAL",
                            TipoSql = "INTEGER",
                            AceitaNulo = true,
                            DefaultSql = null
                        }
                    ]
                }
            ]
        };

        var diff = ComparadorSchema.GerarDiff(origem, alvo);

        Assert.Contains(diff.ComandosSql, sql => sql.Contains("TYPE NUMERIC(15,2)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diff.ComandosSql, sql => sql.Contains("SET NOT NULL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diff.ComandosSql, sql => sql.Contains("SET DEFAULT 0", StringComparison.OrdinalIgnoreCase));
    }
}
