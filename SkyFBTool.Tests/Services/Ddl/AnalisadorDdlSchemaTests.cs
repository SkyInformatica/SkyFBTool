using SkyFBTool.Services.Ddl;
using SkyFBTool.Core;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class AnalisadorDdlSchemaTests
{
    [Fact]
    public async Task AnalisarAsync_SemInputESemDatabase_DeveFalhar()
    {
        var opcoes = new SkyFBTool.Core.OpcoesDdlAnalise();

        await Assert.ThrowsAsync<ArgumentException>(() => AnalisadorDdlSchema.AnalisarAsync(opcoes));
    }

    [Fact]
    public async Task AnalisarAsync_ComInputEDatabase_DeveFalhar()
    {
        var opcoes = new SkyFBTool.Core.OpcoesDdlAnalise
        {
            Entrada = "arquivo.schema.json",
            Database = "C:\\dados\\origem.fdb"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => AnalisadorDdlSchema.AnalisarAsync(opcoes));
    }

    [Fact]
    public async Task AnalisarAsync_ComWildcardNoDatabase_DeveFalhar()
    {
        var opcoes = new SkyFBTool.Core.OpcoesDdlAnalise
        {
            Database = "C:\\dados\\*.fdb"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => AnalisadorDdlSchema.AnalisarAsync(opcoes));
    }

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

        var achado = Assert.Single(resultado.Achados, a =>
            a.Codigo == "FK_SEM_INDICE_COBERTURA" &&
            a.Severidade == "medium");
        Assert.Contains("Child table: PEDIDOS (CLIENTE_ID)", achado.Descricao);
        Assert.Contains("Parent table: CLIENTES (ID)", achado.Descricao);
        Assert.Contains("Create an index on child table PEDIDOS using FK columns (CLIENTE_ID)", achado.Recomendacao);
    }

    [Fact]
    public void Analisar_QuandoFkTemIndiceDeSuporte_NaoDeveGerarAchadoCobertura()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "DEVOLUCAO",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "CODIGODEVOLUCAO", TipoSql = "INTEGER", AceitaNulo = false }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema
                    {
                        Nome = "PK_DEVOLUCAO",
                        Colunas = ["CODIGODEVOLUCAO"]
                    }
                },
                new TabelaSchema
                {
                    Nome = "MOVIMENTO",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "NUMEROLANCAMENTO", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "CODIGODEVOLUCAO", TipoSql = "INTEGER", AceitaNulo = true }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema
                    {
                        Nome = "PK_MOVIMENTO",
                        Colunas = ["NUMEROLANCAMENTO"]
                    },
                    ChavesEstrangeiras =
                    [
                        new ChaveEstrangeiraSchema
                        {
                            Nome = "FK_MOVIMENTO_CODIGODEVOLUCAO",
                            IndiceSuporteNome = "FK_MOVIMENTO_CODIGODEVOLUCAO",
                            Colunas = ["CODIGODEVOLUCAO"],
                            TabelaReferencia = "DEVOLUCAO",
                            ColunasReferencia = ["CODIGODEVOLUCAO"]
                        }
                    ]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.DoesNotContain(resultado.Achados, a => a.Codigo == "FK_SEM_INDICE_COBERTURA");
    }

    [Fact]
    public void Analisar_QuandoColunaIncompativelComVersao_DeveGerarAchado()
    {
        var snapshot = new SnapshotSchema
        {
            VersaoMajor = 2,
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

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        var achado = Assert.Single(resultado.Achados, a => a.Codigo == "CAMPO_TIPO_INCOMPATIVEL_VERSAO");
        Assert.Equal("critical", achado.Severidade);
        Assert.Contains("CLIENTES.ATIVO", achado.Escopo);
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

        var achado = Assert.Single(resultado.Achados, a =>
            a.Codigo == "INDICE_DUPLICADO" &&
            a.Severidade == "low");
        Assert.Contains("Signature: NON-UNIQUE, ASC, (DATA)", achado.Descricao);
    }

    [Fact]
    public void Analisar_QuandoIndiceUsaExpressao_NaoDeveRegistrarColunaInexistente()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "AGRUPAMENTODOCUMENTO",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false },
                        new ColunaSchema { Nome = "DESCRICAO", TipoSql = "VARCHAR(100)", AceitaNulo = true }
                    ],
                    ChavePrimaria = new ChavePrimariaSchema
                    {
                        Nome = "PK_AGRUPAMENTODOCUMENTO",
                        Colunas = ["ID"]
                    },
                    Indices =
                    [
                        new IndiceSchema
                        {
                            Nome = "IND_AGRUPAMENTODOCUMENTO_1",
                            Colunas = ["UPPER(DESCRICAO)"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IND_ARISPARQUIVO_2",
                            Colunas = ["CAST(DATAGERACAO AS DATE)"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IND_BENSINDISPONIVEIS_11",
                            Colunas = ["CASE WHEN COALESCE(CODIGOCNIB, 0) > 0 THEN 'T' ELSE 'F' END"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IND_ENCAMINHAMENTO_24",
                            Colunas = ["DATA + 30"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IND_LOTESELOSSC_12",
                            Colunas = ["NUMEROSEQUENCIALOTE || NUMEROSEQUENCIASELO || DIGITOVERIFICADOR"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IND_LOTESELOSSC_13",
                            Colunas = ["NUMEROSEQUENCIALOTE||NUMEROSEQUENCIASELO||DIGITOVERIFICADOR"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IND_ENCAMINHAMENTO_25",
                            Colunas = ["DATA+30"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IND_PESSOAL_COLLATE",
                            Colunas = ["NOME COLLATE PT_BR"]
                        }
                    ]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.DoesNotContain(resultado.Achados, a => a.Codigo == "INDICE_COLUNA_INEXISTENTE");
    }

    [Fact]
    public void Analisar_QuandoIndiceReferenciaColunaInexistente_DeveRegistrarAchadoHigh()
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
                        new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }
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
                            Nome = "IDX_MOVIMENTO_DATA",
                            Colunas = ["DATA"]
                        }
                    ]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "INDICE_COLUNA_INEXISTENTE" &&
            a.Severidade == "high" &&
            a.Escopo == "MOVIMENTO.IDX_MOVIMENTO_DATA");
    }

    [Fact]
    public void Analisar_QuandoObjetoPsqlSemCorpo_DeveRegistrarAchadoCritico()
    {
        var snapshot = new SnapshotSchema
        {
            Procedimentos =
            [
                new ProcedimentoSchema
                {
                    Nome = "SP_SEM_CORPO",
                    SourceSql = "CREATE OR ALTER PROCEDURE SP_SEM_CORPO (P_ID INTEGER)"
                }
            ],
            Funcoes =
            [
                new FuncaoSchema
                {
                    Nome = "FN_SEM_CORPO",
                    SourceSql = "CREATE OR ALTER FUNCTION FN_SEM_CORPO RETURNS INTEGER"
                }
            ],
            Gatilhos =
            [
                new GatilhoSchema
                {
                    Nome = "TRG_SEM_CORPO",
                    SourceSql = string.Empty
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "PROCEDURE_SEM_CORPO" &&
            a.Severidade == "critical" &&
            a.Escopo == "SP_SEM_CORPO");
        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "FUNCTION_SEM_CORPO" &&
            a.Severidade == "critical" &&
            a.Escopo == "FN_SEM_CORPO");
        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "TRIGGER_SEM_CORPO" &&
            a.Severidade == "critical" &&
            a.Escopo == "TRG_SEM_CORPO");
    }

    [Fact]
    public void Analisar_QuandoObjetoPsqlTemSomenteComentariosNoCorpo_DeveRegistrarAchadoCritico()
    {
        var snapshot = new SnapshotSchema
        {
            Procedimentos =
            [
                new ProcedimentoSchema
                {
                    Nome = "SP_COMENTADA",
                    SourceSql = """
                                CREATE OR ALTER PROCEDURE SP_COMENTADA
                                AS
                                BEGIN
                                  -- rotina desativada temporariamente
                                  /* sem instrução executável */
                                END
                                """
                }
            ],
            Gatilhos =
            [
                new GatilhoSchema
                {
                    Nome = "TRG_COMENTADA",
                    SourceSql = """
                                CREATE OR ALTER TRIGGER TRG_COMENTADA FOR CLIENTES
                                AS
                                BEGIN
                                  /*
                                    NEW.ID = NEW.ID;
                                  */
                                END
                                """
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "PROCEDURE_SEM_CORPO" &&
            a.Severidade == "critical" &&
            a.Escopo == "SP_COMENTADA");
        Assert.Contains(resultado.Achados, a =>
            a.Codigo == "TRIGGER_SEM_CORPO" &&
            a.Severidade == "critical" &&
            a.Escopo == "TRG_COMENTADA");
    }

    [Fact]
    public void Analisar_QuandoObjetoPsqlTemCorpo_NaoDeveRegistrarAchadoSemCorpo()
    {
        var snapshot = new SnapshotSchema
        {
            Procedimentos =
            [
                new ProcedimentoSchema
                {
                    Nome = "SP_OK",
                    SourceSql = "CREATE OR ALTER PROCEDURE SP_OK AS BEGIN SUSPEND; END"
                }
            ],
            Funcoes =
            [
                new FuncaoSchema
                {
                    Nome = "FN_OK",
                    SourceSql = "CREATE OR ALTER FUNCTION FN_OK RETURNS INTEGER AS BEGIN RETURN 1; END"
                }
            ],
            Gatilhos =
            [
                new GatilhoSchema
                {
                    Nome = "TRG_OK",
                    SourceSql = "CREATE OR ALTER TRIGGER TRG_OK FOR CLIENTES AS BEGIN NEW.ID = NEW.ID; END"
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.DoesNotContain(resultado.Achados, a => a.Codigo is "PROCEDURE_SEM_CORPO" or "FUNCTION_SEM_CORPO" or "TRIGGER_SEM_CORPO");
    }

    [Fact]
    public void Analisar_QuandoIndicePrefixoRedundante_DeveRegistrarAchadoMedium()
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
                        new ColunaSchema { Nome = "DATA", TipoSql = "DATE", AceitaNulo = false },
                        new ColunaSchema { Nome = "TIPO", TipoSql = "VARCHAR(10)", AceitaNulo = false }
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
                            Nome = "IDX_MOV_DATA",
                            Unico = false,
                            Descendente = false,
                            Colunas = ["DATA"]
                        },
                        new IndiceSchema
                        {
                            Nome = "IDX_MOV_DATA_TIPO",
                            Unico = false,
                            Descendente = false,
                            Colunas = ["DATA", "TIPO"]
                        }
                    ]
                }
            ]
        };

        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        var achado = Assert.Single(resultado.Achados, a =>
            a.Codigo == "INDICE_REDUNDANTE_PREFIXO" &&
            a.Severidade == "medium");
        Assert.Contains("IDX_MOV_DATA (NON-UNIQUE, ASC, (DATA))", achado.Descricao);
        Assert.Contains("IDX_MOV_DATA_TIPO (NON-UNIQUE, ASC, (DATA, TIPO))", achado.Descricao);
        Assert.Contains("prefix (DATA)", achado.Descricao);
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
