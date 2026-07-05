using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Integration;

public class DdlExtractIntegrationTests
{
    [Fact]
    public async Task DdlExtract_GeraSnapshotJson_ComTabelasColunasPkFkIndiceDominioSequenciaEView()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "ddl_extract_json.fdb");
        string saidaBase = Path.Combine(pastaTemp, "schema_origem");

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            bool suportaFuncoes = await CriarSchemaTesteAsync(arquivoBanco, "UTF8");

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

            var (_, arquivoJson, arquivoAuditoria) = await ExtratorDdlFirebird.ExtrairAsync(opcoes);

            Assert.True(File.Exists(arquivoJson));
            Assert.True(File.Exists(arquivoAuditoria));
            string textoJson = await File.ReadAllTextAsync(arquivoJson);
            string textoAuditoria = await File.ReadAllTextAsync(arquivoAuditoria);
            var snapshot = JsonSerializer.Deserialize<SnapshotSchema>(textoJson);
            Assert.NotNull(snapshot);
            Assert.Contains("\"TotalItens\"", textoAuditoria);

            var clientes = snapshot!.Tabelas.Single(t => t.Nome == "CLIENTES");
            var pedidos = snapshot.Tabelas.Single(t => t.Nome == "PEDIDOS");

            Assert.Contains(snapshot.Dominios, d => d.Nome == "DM_EMAIL" &&
                                                    d.TipoSql.StartsWith("VARCHAR(", StringComparison.OrdinalIgnoreCase) &&
                                                    string.Equals(d.DefaultSql, "DEFAULT 'N/A'", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(snapshot.Sequencias, s => s.Nome == "SEQ_PEDIDOS");
            Assert.Contains(snapshot.Procedimentos, p => p.Nome == "SP_AJUSTAR_PEDIDO");
            Assert.Contains(snapshot.Gatilhos, g => g.Nome == "TRG_PEDIDOS_BI");
            if (suportaFuncoes)
                Assert.Contains(snapshot.Funcoes, f => f.Nome == "FN_PEDIDO_TOTAL");
            Assert.Contains(snapshot.Views, v => v.Nome == "VW_CLIENTES" &&
                                                 v.SelectSql.Contains("SELECT ID, EMAIL FROM CLIENTES", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(clientes.Colunas, c => c.Nome == "ID" && c.TipoSql == "INTEGER" && !c.AceitaNulo);
            Assert.Contains(clientes.Colunas, c => c.Nome == "NOME" && c.TipoSql.StartsWith("VARCHAR(", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(clientes.ChavePrimaria);
            Assert.Equal("PK_CLIENTES", clientes.ChavePrimaria!.Nome);
            Assert.Equal(["ID"], clientes.ChavePrimaria.Colunas);

            Assert.Single(clientes.ChavesUnicas);
            Assert.Equal("UQ_CLIENTES_EMAIL", clientes.ChavesUnicas[0].Nome);
            Assert.Equal(["EMAIL"], clientes.ChavesUnicas[0].Colunas);
            Assert.Single(clientes.RestricoesCheck);
            Assert.Equal("CHK_CLIENTES_EMAIL", clientes.RestricoesCheck[0].Nome);
            Assert.Contains("CONTAINING", clientes.RestricoesCheck[0].CheckSql, StringComparison.OrdinalIgnoreCase);

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
            bool suportaFuncoes = await CriarSchemaTesteAsync(arquivoBanco, "UTF8");

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

            var (arquivoSql, _, arquivoAuditoria) = await ExtratorDdlFirebird.ExtrairAsync(opcoes);
            Assert.True(File.Exists(arquivoSql));
            Assert.True(File.Exists(arquivoAuditoria));

            string sql = await File.ReadAllTextAsync(arquivoSql);
            Assert.Contains("SET NAMES UTF8;", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CHARACTER SET UTF8", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Firebird field compatibility audit", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SET TERM ^;", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SET TERM ;^", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE TABLE \"CLIENTES\"", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE TABLE \"PEDIDOS\"", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE DOMAIN \"DM_EMAIL\" AS VARCHAR(120)", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE SEQUENCE \"SEQ_PEDIDOS\";", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE OR ALTER PROCEDURE \"SP_AJUSTAR_PEDIDO\"", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE OR ALTER TRIGGER \"TRG_PEDIDOS_BI\" FOR \"PEDIDOS\" ACTIVE BEFORE INSERT POSITION 0", sql, StringComparison.OrdinalIgnoreCase);
            if (suportaFuncoes)
                Assert.Contains("CREATE OR ALTER FUNCTION FN_PEDIDO_TOTAL", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE VIEW \"VW_CLIENTES\" AS SELECT ID, EMAIL FROM CLIENTES;", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ALTER TABLE \"CLIENTES\" ADD CONSTRAINT \"PK_CLIENTES\" PRIMARY KEY (\"ID\");", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ALTER TABLE \"CLIENTES\" ADD CONSTRAINT \"UQ_CLIENTES_EMAIL\" UNIQUE (\"EMAIL\");", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ALTER TABLE \"CLIENTES\" ADD CONSTRAINT \"CHK_CLIENTES_EMAIL\" CHECK (EMAIL CONTAINING '@');", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FOREIGN KEY (\"CLIENTE_ID\") REFERENCES \"CLIENTES\" (\"ID\")", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CREATE INDEX \"IDX_PEDIDOS_CLIENTE\" ON \"PEDIDOS\" (\"CLIENTE_ID\");", sql, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task DdlExtract_GeraSql_ComColunaComputada_DevePreservarCoalesce()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "ddl_extract_computed.fdb");
        string saidaBase = Path.Combine(pastaTemp, "schema_computed");

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");

            await using (var conexao = new FbConnection(CriarConnectionString(arquivoBanco, "UTF8")))
            {
                await conexao.OpenAsync();

                const string ddl = """
                                   CREATE TABLE PEDIDOS_COMPUTADOS (
                                       ID INTEGER NOT NULL,
                                       VALOR_TOTAL NUMERIC(15,2),
                                       VALOR_DUPLO COMPUTED BY (COALESCE(VALOR_TOTAL, 0) * 2)
                                   )
                                   """;

                await using var cmd = new FbCommand(ddl, conexao);
                await cmd.ExecuteNonQueryAsync();
            }

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

            var (arquivoSql, arquivoJson, arquivoAuditoria) = await ExtratorDdlFirebird.ExtrairAsync(opcoes);

            string sql = await File.ReadAllTextAsync(arquivoSql);
            Assert.Contains("SET NAMES UTF8;", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CHARACTER SET UTF8", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COALESCE(VALOR_TOTAL, 0) * 2", sql, StringComparison.OrdinalIgnoreCase);

            string textoJson = await File.ReadAllTextAsync(arquivoJson);
            var snapshot = JsonSerializer.Deserialize<SnapshotSchema>(textoJson);
            Assert.NotNull(snapshot);

            var pedidos = snapshot!.Tabelas.Single(t => t.Nome == "PEDIDOS_COMPUTADOS");
            Assert.Contains(pedidos.Colunas, c => c.Nome == "VALOR_DUPLO" &&
                                                  c.ComputedBySql is not null &&
                                                  c.ComputedBySql.Contains("COALESCE", StringComparison.OrdinalIgnoreCase));

            Assert.True(File.Exists(arquivoAuditoria));
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task DdlExtract_QuandoObjetoPsqlTemSourceNulo_DevePreservarNoSnapshot()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "ddl_extract_empty_psql.fdb");
        string saidaBase = Path.Combine(pastaTemp, "schema_empty_psql");

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");

            await using (var conexao = new FbConnection(CriarConnectionString(arquivoBanco, "UTF8")))
            {
                await conexao.OpenAsync();
                await ExecutarAsync(conexao, "CREATE TABLE CLIENTES (ID INTEGER)");
                await ExecutarAsync(conexao, "CREATE OR ALTER PROCEDURE SP_SEM_SOURCE AS BEGIN SUSPEND; END");
                await ExecutarAsync(conexao, "CREATE OR ALTER TRIGGER TRG_SEM_SOURCE FOR CLIENTES AS BEGIN NEW.ID = NEW.ID; END");
                await ExecutarAsync(conexao, "UPDATE RDB$PROCEDURES SET RDB$PROCEDURE_SOURCE = NULL WHERE RDB$PROCEDURE_NAME = 'SP_SEM_SOURCE'");
                await ExecutarAsync(conexao, "UPDATE RDB$TRIGGERS SET RDB$TRIGGER_SOURCE = NULL WHERE RDB$TRIGGER_NAME = 'TRG_SEM_SOURCE'");
            }

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

            var (_, arquivoJson, _) = await ExtratorDdlFirebird.ExtrairAsync(opcoes);

            string textoJson = await File.ReadAllTextAsync(arquivoJson);
            var snapshot = JsonSerializer.Deserialize<SnapshotSchema>(textoJson);
            Assert.NotNull(snapshot);

            Assert.Contains(snapshot!.Procedimentos, p =>
                p.Nome == "SP_SEM_SOURCE" &&
                p.SourceSql == string.Empty);
            Assert.Contains(snapshot.Gatilhos, t =>
                t.Nome == "TRG_SEM_SOURCE" &&
                t.SourceSql == string.Empty);
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

    private static async Task<bool> CriarSchemaTesteAsync(string arquivoBanco, string charset)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        bool suportaFuncoes = int.TryParse(conexao.ServerVersion.Split('.')[0], out int major) && major >= 3;

        var comandos = new List<string>
        {
            "CREATE DOMAIN \"DM_EMAIL\" AS VARCHAR(120) DEFAULT 'N/A' NOT NULL CHECK (VALUE CONTAINING '@')",
            "CREATE SEQUENCE \"SEQ_PEDIDOS\"",
            "CREATE TABLE CLIENTES (\n  ID INTEGER NOT NULL,\n  NOME VARCHAR(120),\n  EMAIL DM_EMAIL,\n  CONSTRAINT PK_CLIENTES PRIMARY KEY (ID),\n  CONSTRAINT UQ_CLIENTES_EMAIL UNIQUE (EMAIL),\n  CONSTRAINT CHK_CLIENTES_EMAIL CHECK (EMAIL CONTAINING '@')\n)",
            "CREATE TABLE PEDIDOS (\n  ID INTEGER NOT NULL,\n  CLIENTE_ID INTEGER NOT NULL,\n  VALOR_TOTAL NUMERIC(15,2) DEFAULT 0 NOT NULL,\n  CONSTRAINT PK_PEDIDOS PRIMARY KEY (ID),\n  CONSTRAINT FK_PEDIDOS_CLIENTE FOREIGN KEY (CLIENTE_ID) REFERENCES CLIENTES (ID)\n)",
            "CREATE VIEW VW_CLIENTES AS\nSELECT ID, EMAIL\nFROM CLIENTES",
            "CREATE OR ALTER PROCEDURE SP_AJUSTAR_PEDIDO (P_ID INTEGER)\nAS\nBEGIN\n  UPDATE PEDIDOS SET VALOR_TOTAL = VALOR_TOTAL WHERE ID = :P_ID;\nEND",
            "CREATE OR ALTER TRIGGER TRG_PEDIDOS_BI FOR PEDIDOS ACTIVE BEFORE INSERT POSITION 0\nAS\nBEGIN\n  IF (NEW.VALOR_TOTAL IS NULL) THEN NEW.VALOR_TOTAL = 0;\nEND",
            "CREATE INDEX IDX_PEDIDOS_CLIENTE ON PEDIDOS (CLIENTE_ID)"
        };

        if (suportaFuncoes)
        {
            comandos.Insert(5, "CREATE OR ALTER FUNCTION FN_PEDIDO_TOTAL (P_VALOR NUMERIC(15,2))\nRETURNS NUMERIC(15,2)\nAS\nBEGIN\n  RETURN COALESCE(P_VALOR, 0);\nEND");
        }

        foreach (var comando in comandos)
        {
            await ExecutarAsync(conexao, comando);
        }

        return suportaFuncoes;
    }

    private static async Task ExecutarAsync(FbConnection conexao, string sql)
    {
        await using var cmd = new FbCommand(sql, conexao);
        await cmd.ExecuteNonQueryAsync();
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
