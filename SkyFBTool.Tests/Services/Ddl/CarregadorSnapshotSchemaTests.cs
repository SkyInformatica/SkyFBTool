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
                     SET SQL DIALECT 3;
                     CREATE TABLE "CLIENTES" (
                         "ID" INTEGER NOT NULL,
                         "NOME" VARCHAR(120)
                     );
                     CREATE TABLE "PEDIDOS" (
                         "ID" INTEGER NOT NULL,
                         "CLIENTE_ID" INTEGER NOT NULL,
                         CONSTRAINT "PK_PEDIDOS" PRIMARY KEY ("ID")
                     );
                     ALTER TABLE "CLIENTES" ADD CONSTRAINT "PK_CLIENTES" PRIMARY KEY ("ID");
                     ALTER TABLE "PEDIDOS" ADD CONSTRAINT "FK_PEDIDOS_CLIENTES" FOREIGN KEY ("CLIENTE_ID")
                         REFERENCES "CLIENTES" ("ID") ON UPDATE CASCADE ON DELETE RESTRICT;
                     CREATE INDEX "IDX_PEDIDOS_CLIENTE" ON "PEDIDOS" ("CLIENTE_ID");
                     """;

        await File.WriteAllTextAsync(arquivoSql, ddl);

        var (snapshot, origem) = await CarregadorSnapshotSchema.CarregarSnapshotComOrigemAsync(arquivoSql);

        Assert.Equal(arquivoSql, origem);
        Assert.Equal(2, snapshot.Tabelas.Count);

        var clientes = snapshot.Tabelas.Single(t => t.Nome == "CLIENTES");
        Assert.Equal("PK_CLIENTES", clientes.ChavePrimaria?.Nome);
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
