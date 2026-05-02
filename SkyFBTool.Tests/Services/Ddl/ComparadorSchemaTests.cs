using SkyFBTool.Services.Ddl;
using SkyFBTool.Core;
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

    [Fact]
    public void GerarDiff_QuandoIndiceDuplicadoNaOrigem_DeveIncluirNomesNoAviso()
    {
        var origem = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "AVISO",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "CODIGOAVISO", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    Indices =
                    [
                        new IndiceSchema
                        {
                            Nome = "IDX_AVISO_A",
                            Unico = false,
                            Descendente = false,
                            Colunas = ["CODIGOAVISO"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IDX_AVISO_B",
                            Unico = false,
                            Descendente = false,
                            Colunas = ["CODIGOAVISO"]
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
                    Nome = "AVISO",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "CODIGOAVISO", TipoSql = "INTEGER", AceitaNulo = false }
                    ]
                }
            ]
        };

        var diff = ComparadorSchema.GerarDiff(origem, alvo);

        Assert.Contains(diff.Avisos, aviso =>
            aviso.Contains("IDX_AVISO_A", StringComparison.OrdinalIgnoreCase) &&
            aviso.Contains("IDX_AVISO_B", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GerarDiff_QuandoTabelaComFkParaOutraNova_DeveOrdenarFkAposCriacoes()
    {
        var origem = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "FILHO",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "PAI_ID", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema { Nome = "PK_FILHO", Colunas = ["ID"] },
                    ChavesEstrangeiras =
                    [
                        new ChaveEstrangeiraSchema
                        {
                            Nome = "FK_FILHO_PAI",
                            Colunas = ["PAI_ID"],
                            TabelaReferencia = "PAI",
                            ColunasReferencia = ["ID"]
                        }
                    ]
                },
                new TabelaSchema
                {
                    Nome = "PAI",
                    Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }],
                    ChavePrimaria = new ChavePrimariaSchema { Nome = "PK_PAI", Colunas = ["ID"] }
                }
            ]
        };

        var alvo = new SnapshotSchema();

        var diff = ComparadorSchema.GerarDiff(origem, alvo);

        int indiceCreatePai = diff.ComandosSql.FindIndex(sql => sql.Contains("CREATE TABLE \"PAI\"", StringComparison.OrdinalIgnoreCase));
        int indiceCreateFilho = diff.ComandosSql.FindIndex(sql => sql.Contains("CREATE TABLE \"FILHO\"", StringComparison.OrdinalIgnoreCase));
        int indicePkPai = diff.ComandosSql.FindIndex(sql => sql.Contains("ALTER TABLE \"PAI\" ADD CONSTRAINT \"PK_PAI\" PRIMARY KEY", StringComparison.OrdinalIgnoreCase));
        int indiceFkFilhoPai = diff.ComandosSql.FindIndex(sql => sql.Contains("ALTER TABLE \"FILHO\" ADD CONSTRAINT \"FK_FILHO_PAI\" FOREIGN KEY", StringComparison.OrdinalIgnoreCase));

        Assert.True(indiceCreatePai >= 0);
        Assert.True(indiceCreateFilho >= 0);
        Assert.True(indicePkPai >= 0);
        Assert.True(indiceFkFilhoPai >= 0);
        Assert.True(indiceFkFilhoPai > indiceCreatePai);
        Assert.True(indiceFkFilhoPai > indiceCreateFilho);
        Assert.True(indiceFkFilhoPai > indicePkPai);
    }
}
