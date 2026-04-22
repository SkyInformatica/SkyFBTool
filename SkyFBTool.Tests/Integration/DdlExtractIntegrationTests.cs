using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Integration;

public class DdlExtractIntegrationTests
{
    [Fact]
    public async Task DdlExtract_GeraSnapshotJson_ComTabelasColunasPkFkEIndice()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "ddl_extract_json.fdb");
        string saidaBase = Path.Combine(pastaTemp, "schema_origem");

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarSchemaTesteAsync(arquivoBanco, "UTF8");

            var opcoes = new OpcoesDdlExtracao
            {
                Host = ObterHost(),
                Porta = ObterPorta(),
                Usuario = ObterUsuario(),
                Senha = ObterSenha(),
                Database = arquivoBanco,
                Charset = "UTF8",
                Saida = saidaBase
            };

            var (_, arquivoJson) = await ExtratorDdlFirebird.ExtrairAsync(opcoes);

            Assert.True(File.Exists(arquivoJson));
            string textoJson = await File.ReadAllTextAsync(arquivoJson);
            var snapshot = JsonSerializer.Deserialize<SnapshotSchema>(textoJson);
            Assert.NotNull(snapshot);

            var clientes = snapshot!.Tabelas.Single(t => t.Nome == "CLIENTES");
            var pedidos = snapshot.Tabelas.Single(t => t.Nome == "PEDIDOS");

            Assert.Contains(clientes.Colunas, c => c.Nome == "ID" && c.TipoSql == "INTEGER" && !c.AceitaNulo);
            Assert.Contains(clientes.Colunas, c => c.Nome == "NOME" && c.TipoSql.StartsWith("VARCHAR(", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(clientes.ChavePrimaria);
            Assert.Equal("PK_CLIENTES", clientes.ChavePrimaria!.Nome);
            Assert.Equal(["ID"], clientes.ChavePrimaria.Colunas);

            Assert.Contains(pedidos.Colunas, c => c.Nome == "VALOR_TOTAL" &&
                                                  c.TipoSql.StartsWith("NUMERIC(", StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(c.DefaultSql, "DEFAULT 0", StringComparison.OrdinalIgnoreCase));

            var fk = Assert.Single(pedidos.ChavesEstrangeiras);
            Assert.Equal("FK_PEDIDOS_CLIENTE", fk.Nome);
            Assert.Equal("CLIENTES", fk.TabelaReferencia);
            Assert.Equal(["CLIENTE_ID"], fk.Colunas);
            Assert.Equal(["ID"], fk.ColunasReferencia);

            var idx = Assert.Single(pedidos.Indices);
            Assert.Equal("IDX_PEDIDOS_CLIENTE", idx.Nome);
            Assert.Equal(["CLIENTE_ID"], idx.Colunas);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task DdlExtract_GeraSql_ComCreateTablePkFkEIndice()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "ddl_extract_sql.fdb");
        string saidaBase = Path.Combine(pastaTemp, "schema_origem");

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarSchemaTesteAsync(arquivoBanco, "UTF8");

            var opcoes = new OpcoesDdlExtracao
            {
                Host = ObterHost(),
                Porta = ObterPorta(),
                Usuario = ObterUsuario(),
                Senha = ObterSenha(),
                Database = arquivoBanco,
                Charset = "UTF8",
                Saida = saidaBase
            };

            var (arquivoSql, _) = await ExtratorDdlFirebird.ExtrairAsync(opcoes);
            Assert.True(File.Exists(arquivoSql));

            string sql = await File.ReadAllTextAsync(arquivoSql);
            Assert.Contains("CREATE TABLE \"CLIENTES\"", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE TABLE \"PEDIDOS\"", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ALTER TABLE \"CLIENTES\" ADD CONSTRAINT \"PK_CLIENTES\" PRIMARY KEY (\"ID\");", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FOREIGN KEY (\"CLIENTE_ID\") REFERENCES \"CLIENTES\" (\"ID\")", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE INDEX \"IDX_PEDIDOS_CLIENTE\" ON \"PEDIDOS\" (\"CLIENTE_ID\");", sql, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    private static bool IntegracaoHabilitada()
    {
        string? flag = Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_RUN_INTEGRATION");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CriarBancoAsync(string arquivoBanco, string charset)
    {
        var csb = new FbConnectionStringBuilder
        {
            DataSource = ObterHost(),
            Port = ObterPorta(),
            UserID = ObterUsuario(),
            Password = ObterSenha(),
            Database = arquivoBanco,
            Charset = charset,
            Dialect = 3
        };

        FbConnection.CreateDatabase(csb.ConnectionString, pageSize: 8192, forcedWrites: false, overwrite: true);
        await Task.CompletedTask;
    }

    private static async Task CriarSchemaTesteAsync(string arquivoBanco, string charset)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        string sql = """
                     CREATE TABLE CLIENTES (
                       ID INTEGER NOT NULL,
                       NOME VARCHAR(120),
                       CONSTRAINT PK_CLIENTES PRIMARY KEY (ID)
                     );

                     CREATE TABLE PEDIDOS (
                       ID INTEGER NOT NULL,
                       CLIENTE_ID INTEGER NOT NULL,
                       VALOR_TOTAL NUMERIC(15,2) DEFAULT 0 NOT NULL,
                       CONSTRAINT PK_PEDIDOS PRIMARY KEY (ID),
                       CONSTRAINT FK_PEDIDOS_CLIENTE FOREIGN KEY (CLIENTE_ID) REFERENCES CLIENTES (ID)
                     );

                     CREATE INDEX IDX_PEDIDOS_CLIENTE ON PEDIDOS (CLIENTE_ID);
                     """;

        foreach (var comando in sql.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string atual = comando.Trim();
            if (string.IsNullOrWhiteSpace(atual))
                continue;

            await using var cmd = new FbCommand(atual, conexao);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string CriarConnectionString(string arquivoBanco, string charset)
    {
        var csb = new FbConnectionStringBuilder
        {
            DataSource = ObterHost(),
            Port = ObterPorta(),
            UserID = ObterUsuario(),
            Password = ObterSenha(),
            Database = arquivoBanco,
            Charset = charset,
            Dialect = 3
        };

        return csb.ConnectionString;
    }

    private static string ObterHost()
    {
        return Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_HOST") ?? "localhost";
    }

    private static int ObterPorta()
    {
        string? porta = Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_PORT");
        return int.TryParse(porta, out int valor) ? valor : 3050;
    }

    private static string ObterUsuario()
    {
        return Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_USER") ?? "sysdba";
    }

    private static string ObterSenha()
    {
        return Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_PASSWORD") ?? "masterkey";
    }

    private static string CriarPastaTemp()
    {
        string pasta = Path.Combine(Path.GetTempPath(), "SkyFBTool.Tests.Integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    private static void TentarExcluirDiretorio(string pasta)
    {
        if (!Directory.Exists(pasta))
            return;

        try
        {
            Directory.Delete(pasta, recursive: true);
        }
        catch
        {
        }
    }
}
