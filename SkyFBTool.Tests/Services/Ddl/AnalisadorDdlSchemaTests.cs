using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class AnalisadorDdlSchemaTests
{
    [Fact]
    public void Analisar_QuandoPkReferenciaColunaInexistente_DeveMarcarComoCritico()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema
                    {
                        Nome = "PK_CLIENTES",
                        Colunas = ["CODIGO"]
                    }
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "PK_REFERENCIA_COLUNA_INEXISTENTE" &&
            a.Severidade == "critical");
    }

    [Fact]
    public void Analisar_QuandoFkSemIndiceCobertura_DeveGerarAchadoMedio()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema
                    {
                        Nome = "PK_CLIENTES",
                        Colunas = ["ID"]
                    }
                },
                new TabelaSchema
                {
                    Nome = "PEDIDOS",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "CLIENTE_ID", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema
                    {
                        Nome = "PK_PEDIDOS",
                        Colunas = ["ID"]
                    },
                    ChavesEstrangeiras =
                    [
                        new ChaveEstrangeiraSchema
                        {
                            Nome = "FK_PEDIDOS_CLIENTES",
                            Colunas = ["CLIENTE_ID"],
                            TabelaReferencia = "CLIENTES",
                            ColunasReferencia = ["ID"]
                        }
                    ]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "FK_SEM_INDICE_COBERTURA" &&
            a.Severidade == "medium");
    }

    [Fact]
    public void Analisar_QuandoIndiceDuplicado_DeveRegistrarAchado()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "MOVIMENTO",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "DATA", TipoSql = "DATE", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema
                    {
                        Nome = "PK_MOVIMENTO",
                        Colunas = ["ID"]
                    },
                    Indices =
                    [
                        new IndiceSchema
                        {
                            Nome = "IDX_MOV_01",
                            Unico = false,
                            Descendente = false,
                            Colunas = ["DATA"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IDX_MOV_02",
                            Unico = false,
                            Descendente = false,
                            Colunas = ["DATA"]
                        }
                    ]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "INDICE_DUPLICADO" &&
            a.Severidade == "medium");
    }
}
