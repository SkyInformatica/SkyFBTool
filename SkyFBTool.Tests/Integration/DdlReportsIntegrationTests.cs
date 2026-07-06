using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Cli.Commands;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Integration;

public class DdlReportsIntegrationTests
{
    [Fact]
    public async Task DdlAnalyze_DatabasesBatch_ComBaseSemAchados_DeveMarcarNaoAplicavelNoResumo()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string pastaSaida = Path.Combine(pastaTemp, "saida");
        Directory.CreateDirectory(pastaSaida);

        string arquivoBancoComAchado = Path.Combine(pastaTemp, "db_com_achado.fdb");
        string arquivoBancoSemAchado = Path.Combine(pastaTemp, "db_sem_achado.fdb");

        try
        {
            await CriarBancoAsync(arquivoBancoComAchado, "UTF8");
            await CriarBancoAsync(arquivoBancoSemAchado, "UTF8");

            await ExecutarSqlAsync(
                arquivoBancoComAchado,
                "UTF8",
                "CREATE TABLE CLIENTE_SEM_PK (ID INTEGER, NOME VARCHAR(60));");

            await ExecutarSqlAsync(
                arquivoBancoSemAchado,
                "UTF8",
                "CREATE TABLE CLIENTE_OK (ID INTEGER NOT NULL, NOME VARCHAR(60), CONSTRAINT PK_CLIENTE_OK PRIMARY KEY (ID));");

            await DdlAnalyzeCommand.ExecuteAsync(
            [
                "--databases-batch", Path.Combine(pastaTemp, "*.fdb"),
                "--output", pastaSaida + Path.DirectorySeparatorChar
            ]);

            string arquivoResumoJson = Directory
                .GetFiles(pastaSaida, "batch_analysis_summary_*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(a => a, StringComparer.OrdinalIgnoreCase)
                .First();
            Assert.Single(Directory.GetFiles(pastaSaida, "db_com_achado_schema_analysis_*.json", SearchOption.TopDirectoryOnly));
            Assert.Single(Directory.GetFiles(pastaSaida, "db_com_achado_schema_analysis_*.html", SearchOption.TopDirectoryOnly));
            Assert.Single(Directory.GetFiles(pastaSaida, "db_sem_achado_schema_analysis_*.json", SearchOption.TopDirectoryOnly));
            Assert.Single(Directory.GetFiles(pastaSaida, "db_sem_achado_schema_analysis_*.html", SearchOption.TopDirectoryOnly));

            var resumo = JsonSerializer.Deserialize<ResultadoResumoLoteDdl>(await File.ReadAllTextAsync(arquivoResumoJson));
            Assert.NotNull(resumo);

            var baseSemAchado = resumo!.Bases.FirstOrDefault(b =>
                string.Equals(b.NomeBase, "db_sem_achado", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(baseSemAchado);
            Assert.Equal(0, baseSemAchado!.TotalAchados);
            Assert.Equal("none", baseSemAchado.MaiorSeveridade);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task DdlDiff_GeraHtmlComEstilosDeImpressaoEIndicadoresVisuais()
    {
        string pastaTemp = CriarPastaTemp();
        string arquivoOrigem = Path.Combine(pastaTemp, "origem.sql");
        string arquivoAlvo = Path.Combine(pastaTemp, "alvo.sql");
        string saida = Path.Combine(pastaTemp, "diff_saida");

        try
        {
            await File.WriteAllTextAsync(arquivoOrigem, """
SET SQL DIALECT 3;
CREATE TABLE CLIENTES (
  ID INTEGER NOT NULL,
  NOME VARCHAR(80),
  CONSTRAINT PK_CLIENTES PRIMARY KEY (ID)
);
""");

            await File.WriteAllTextAsync(arquivoAlvo, """
SET SQL DIALECT 3;
CREATE TABLE CLIENTES (
  ID INTEGER NOT NULL,
  CONSTRAINT PK_CLIENTES PRIMARY KEY (ID)
);
""");

            await DdlDiffCommand.ExecuteAsync(
            [
                "--source", arquivoOrigem,
                "--target", arquivoAlvo,
                "--output", saida
            ]);

            string arquivoHtml = $"{saida}.html";
            Assert.True(File.Exists(arquivoHtml));

            string html = await File.ReadAllTextAsync(arquivoHtml);
            Assert.Contains("@media print", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("print-color-adjust: exact", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".pill.created", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".pill.altered", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".pill.target-only", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task DdlAnalyze_ComDescricaoAcentuada_DevePreservarNoJsonEHtml()
    {
        string pastaTemp = CriarPastaTemp();
        string arquivoEntrada = Path.Combine(pastaTemp, "entrada.sql");
        string saida = Path.Combine(pastaTemp, "analise_acentos");
        const string descricao = "Análise de integridade: ação, coração, órgão, êxito e infância.";

        try
        {
            await File.WriteAllTextAsync(arquivoEntrada, """
SET SQL DIALECT 3;
CREATE TABLE CLIENTE_SEM_PK (
  ID INTEGER,
  NOME VARCHAR(60)
);
""");

            await DdlAnalyzeCommand.ExecuteAsync(
            [
                "--input", arquivoEntrada,
                "--description", descricao,
                "--output", saida
            ]);

            string arquivoJson = $"{saida}.json";
            string arquivoHtml = $"{saida}.html";

            Assert.True(File.Exists(arquivoJson));
            Assert.True(File.Exists(arquivoHtml));

            string json = await File.ReadAllTextAsync(arquivoJson);
            string html = await File.ReadAllTextAsync(arquivoHtml);

            var resultado = JsonSerializer.Deserialize<ResultadoAnaliseDdl>(json);
            Assert.NotNull(resultado);
            Assert.Equal(descricao, resultado!.Description);
            Assert.True(
                html.Contains("Análise de integridade", StringComparison.Ordinal) ||
                html.Contains("An&#225;lise de integridade", StringComparison.Ordinal));
            Assert.True(
                html.Contains("ação", StringComparison.Ordinal) ||
                html.Contains("a&#231;&#227;o", StringComparison.Ordinal));
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

    private static async Task ExecutarSqlAsync(string arquivoBanco, string charset, string sql)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

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
