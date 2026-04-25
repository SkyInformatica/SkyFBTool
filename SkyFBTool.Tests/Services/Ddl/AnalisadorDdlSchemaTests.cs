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
    public void Analisar_QuandoIndiceDuplicado_DeveRegistrarAchadoLow()
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
            a.Severidade == "low");
    }

    [Fact]
    public void Analisar_ComPrefixoIgnorado_DeveExcluirTabelaDoResultado()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "LOG_EVENTOS",
                    Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }]
                },
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot, prefixosTabelaIgnorados: ["LOG_"]);

        Assert.Equal(1, resultado.TotalTabelas);
        Assert.DoesNotContain(resultado.Achados, a => a.Escopo.StartsWith("LOG_EVENTOS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analisar_DevePreencherResumoPorCodigoETabela()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "A",
                    Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }]
                },
                new TabelaSchema
                {
                    Nome = "B",
                    Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.NotEmpty(resultado.ResumoPorCodigo);
        Assert.NotEmpty(resultado.ResumoPorTabela);
        Assert.Contains(resultado.ResumoPorCodigo, i => i.Chave == "TABELA_SEM_PK");
        Assert.Contains(resultado.ResumoPorTabela, i => i.Chave == "A" || i.Chave == "B");
    }

    [Fact]
    public void Analisar_ComOverrideDeSeveridade_DeveAplicarNivelConfigurado()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "SEM_PK",
                    Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }]
                }
            ]
        };

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TABELA_SEM_PK"] = "critical"
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot, severidadesOverride: overrides);

        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "TABELA_SEM_PK" &&
            a.Severidade == "critical");
    }
}
