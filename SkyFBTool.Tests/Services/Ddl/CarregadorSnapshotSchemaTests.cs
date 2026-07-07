using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class CarregadorSnapshotSchemaTests
{
    [Fact]
    public async Task CarregarSnapshotComOrigemAsync_QuandoSqlSemSchemaJson_DeveParsearDdl()
    {
        string pasta = CriarPastaTemporaria();
        string arquivoSql = Path.Combine(pasta, "origem.sql");

        string ddl = """
                     CREATE DOMAIN "DM_EMAIL" AS VARCHAR(120) DEFAULT 'N/A' NOT NULL CHECK (VALUE CONTAINING '@');
                     CREATE SEQUENCE "SEQ_PEDIDOS";
                     SET TERM ^;
                     CREATE OR ALTER PROCEDURE "SP_AJUSTAR_PEDIDO" (P_ID INTEGER)
                     AS
                     BEGIN
                         UPDATE "PEDIDOS" SET "VALOR_TOTAL" = "VALOR_TOTAL" WHERE "ID" = :P_ID;
                     END ^
                     CREATE OR ALTER FUNCTION "FN_PEDIDO_TOTAL" (P_VALOR NUMERIC(15,2))
                     RETURNS NUMERIC(15,2)
                     AS
                     BEGIN
                         RETURN COALESCE(P_VALOR, 0);
                     END ^
                     CREATE OR ALTER TRIGGER "TRG_PEDIDOS_BI" FOR "PEDIDOS" ACTIVE BEFORE INSERT POSITION 0
                     AS
                     BEGIN
                         IF (NEW."VALOR_TOTAL" IS NULL) THEN NEW."VALOR_TOTAL" = 0;
                     END ^
                     SET TERM ;^
                     DECLARE EXTERNAL FUNCTION "UDF_EXEMPLO"
                     (
                         CSTRING(80) BY DESCRIPTOR,
                         INTEGER
                     )
                     RETURNS CSTRING(80)
                     ENTRY_POINT 'udf_exemplo'
                     MODULE_NAME 'udf_lib';
                     SET SQL DIALECT 3;
                     CREATE TABLE "CLIENTES" (
                         "ID" INTEGER NOT NULL,
                         "NOME" VARCHAR(120),
                         "EMAIL" DM_EMAIL,
                         CONSTRAINT "UQ_CLIENTES_EMAIL" UNIQUE ("EMAIL"),
                         CONSTRAINT "CHK_CLIENTES_EMAIL" CHECK ("EMAIL" CONTAINING '@')
                     );
                     CREATE TABLE "PEDIDOS" (
                         "ID" INTEGER NOT NULL,
                         "CLIENTE_ID" INTEGER NOT NULL,
                         CONSTRAINT "PK_PEDIDOS" PRIMARY KEY ("ID")
                     );
                     CREATE VIEW "VW_CLIENTES" AS
                     SELECT "CLIENTES"."ID", "CLIENTES"."EMAIL"
                     FROM "CLIENTES";
                     ALTER TABLE "CLIENTES" ADD CONSTRAINT "PK_CLIENTES" PRIMARY KEY ("ID");
                     ALTER TABLE "PEDIDOS" ADD CONSTRAINT "FK_PEDIDOS_CLIENTES" FOREIGN KEY ("CLIENTE_ID")
                         REFERENCES "CLIENTES" ("ID") ON UPDATE CASCADE ON DELETE RESTRICT;
                     CREATE INDEX "IDX_PEDIDOS_CLIENTE" ON "PEDIDOS" ("CLIENTE_ID");
                     """;

        await File.WriteAllTextAsync(arquivoSql, ddl);

        var (snapshot, origem) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(arquivoSql);

        Assert.Equal(arquivoSql, origem);
        Assert.Equal(2, snapshot.Tabelas.Count);
        Assert.Contains(snapshot.Dominios, d => d.Nome == "DM_EMAIL" &&
                                                d.TipoSql.StartsWith("VARCHAR(", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshot.Sequencias, s => s.Nome == "SEQ_PEDIDOS");
        Assert.Contains(snapshot.Procedimentos, p => p.Nome == "SP_AJUSTAR_PEDIDO");
        Assert.Contains(snapshot.Funcoes, f => f.Nome == "FN_PEDIDO_TOTAL");
        Assert.Contains(snapshot.FuncoesExternas, f => f.Nome == "UDF_EXEMPLO");
        Assert.Contains(snapshot.Gatilhos, g => g.Nome == "TRG_PEDIDOS_BI");
        Assert.Single(snapshot.Views);
        Assert.Equal("VW_CLIENTES", snapshot.Views[0].Nome);
        Assert.Contains("SELECT", snapshot.Views[0].SelectSql, StringComparison.OrdinalIgnoreCase);

        var clientes = snapshot.Tabelas.Single(t => t.Nome == "CLIENTES");
        Assert.Equal("PK_CLIENTES", clientes.ChavePrimaria?.Nome);
        Assert.Single(clientes.ChavesUnicas);
        Assert.Equal("UQ_CLIENTES_EMAIL", clientes.ChavesUnicas[0].Nome);
        Assert.Single(clientes.RestricoesCheck);
        Assert.Equal("CHK_CLIENTES_EMAIL", clientes.RestricoesCheck[0].Nome);
        Assert.Contains(clientes.Colunas, c => c.Nome == "ID" && c.TipoSql.StartsWith("INTEGER", StringComparison.OrdinalIgnoreCase));

        var pedidos = snapshot.Tabelas.Single(t => t.Nome == "PEDIDOS");
        Assert.Single(pedidos.ChavesEstrangeiras);
        Assert.Contains(pedidos.Indices, i => i.Nome == "IDX_PEDIDOS_CLIENTE");
    }

    [Fact]
    public async Task CarregarSnapshotComOrigemAsync_QuandoSqlETemSchemaJson_DevePriorizarSchemaJson()
    {
        string pasta = CriarPastaTemporaria();
        string arquivoSql = Path.Combine(pasta, "origem.sql");
        string arquivoJson = Path.Combine(pasta, "origem.schema.json");

        await File.WriteAllTextAsync(arquivoSql, "CREATE TABLE \"IGNORAR\" (\"ID\" INTEGER);");

        var snapshotJson = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "VINDO_DO_JSON",
                    Colunas = [new ColunaSchema { Nome = "ID", TipoSql = "INTEGER", AceitaNulo = false }]
                }
            ]
        };

        await File.WriteAllTextAsync(
            arquivoJson,
            System.Text.Json.JsonSerializer.Serialize(snapshotJson, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        var (snapshot, origem) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(arquivoSql);

        Assert.Equal(arquivoJson, origem);
        Assert.Single(snapshot.Tabelas);
        Assert.Equal("VINDO_DO_JSON", snapshot.Tabelas[0].Nome);
    }

    [Fact]
    public async Task CarregarSnapshotComOrigemAsync_QuandoTabelaTemporariaGlobal_DeveParsearColunas()
    {
        string pasta = CriarPastaTemporaria();
        string arquivoSql = Path.Combine(pasta, "origem.sql");

        string ddl = """
                     CREATE GLOBAL TEMPORARY TABLE TMP_PROTOCOLOSENTRADA (
                         DATA DATE,
                         CODIGOUSUARIO INTEGER,
                         SEQUENCIA INTEGER,
                         PROTOCOLO INTEGER
                     ) ON COMMIT PRESERVE ROWS;
                     CREATE INDEX IND_TMP_PROTOCOLOSENTRADA_1 ON TMP_PROTOCOLOSENTRADA (DATA, CODIGOUSUARIO);
                     """;

        await File.WriteAllTextAsync(arquivoSql, ddl);

        var (snapshot, _) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(arquivoSql);

        var tabela = Assert.Single(snapshot.Tabelas);
        Assert.Equal("TMP_PROTOCOLOSENTRADA", tabela.Nome);
        Assert.Contains(tabela.Colunas, c => c.Nome == "DATA");
        Assert.Contains(tabela.Colunas, c => c.Nome == "CODIGOUSUARIO");
        Assert.Contains(tabela.Indices, i => i.Nome == "IND_TMP_PROTOCOLOSENTRADA_1");
    }

    [Fact]
    public async Task AnalisarAsync_QuandoEntradaSqlPuro_DeveGerarRelatorio()
    {
        string pasta = CriarPastaTemporaria();
        string arquivoSql = Path.Combine(pasta, "schema_firebird.sql");
        string saidaBase = Path.Combine(pasta, "saida", "analise");

        string ddl = """
                     CREATE TABLE "SEM_PK" (
                         "ID" INTEGER
                     );
                     """;
        await File.WriteAllTextAsync(arquivoSql, ddl);

        var opcoes = new OpcoesDdlAnalise
        {
            Entrada = arquivoSql,
            Saida = saidaBase
        };

        var (arquivoJson, arquivoHtml) = await AnalisadorDdlSchema.AnalisarAsync(opcoes);

        Assert.True(File.Exists(arquivoJson));
        Assert.True(File.Exists(arquivoHtml));
    }

    [Fact]
    public async Task CarregarSnapshotComOrigemAsync_QuandoSqlTemStubPsqlAntesDoCorpoFinal_DeveUsarUltimaDefinicao()
    {
        string pasta = CriarPastaTemporaria();
        string arquivoSql = Path.Combine(pasta, "schema_extraido.sql");

        string ddl = """
                     SET TERM ^;

                     CREATE PROCEDURE "SP_DEPENDENTE"
                     RETURNS (
                         "RESULTADO" INTEGER
                     )
                     AS
                     BEGIN
                     END ^

                     CREATE TRIGGER "TRG_CLIENTES_BI" FOR "CLIENTES" ACTIVE BEFORE INSERT POSITION 0
                     AS
                     BEGIN
                     END ^

                     ALTER PROCEDURE "SP_DEPENDENTE"
                     RETURNS (
                         "RESULTADO" INTEGER
                     )
                     AS
                     BEGIN
                         RESULTADO = 1;
                         SUSPEND;
                     END ^

                     ALTER TRIGGER "TRG_CLIENTES_BI" ACTIVE BEFORE INSERT POSITION 0
                     AS
                     BEGIN
                         NEW."ID" = COALESCE(NEW."ID", 1);
                     END ^

                     SET TERM ;^
                     """;

        await File.WriteAllTextAsync(arquivoSql, ddl);

        var (snapshot, origem) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(arquivoSql);
        var resultado = AnalisadorDdlSchema.Analisar(snapshot);

        Assert.Equal(arquivoSql, origem);
        var procedimento = Assert.Single(snapshot.Procedimentos);
        var gatilho = Assert.Single(snapshot.Gatilhos);
        Assert.Contains("RESULTADO = 1", procedimento.SourceSql);
        Assert.Contains("COALESCE", gatilho.SourceSql);
        Assert.DoesNotContain(resultado.Achados, a =>
            a.Codigo is "PROCEDURE_SEM_CORPO" or "TRIGGER_SEM_CORPO" or "PROCEDURE_SOMENTE_SUSPEND" or "TRIGGER_SOMENTE_SUSPEND");
    }

    private static string CriarPastaTemporaria()
    {
        string pasta = Path.Combine(
            Path.GetTempPath(),
            "SkyFBTool.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }
}
