using SkyFBTool.Cli.Commands;
using Xunit;

namespace SkyFBTool.Tests.Cli.Commands;

public class CreateDatabaseCommandTests
{
    [Fact]
    public async Task ExecuteAsync_SemDatabase_DeveLancarExcecao()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateDatabaseCommand.ExecuteAsync([]));

        Assert.True(
            ex.Message.Contains("obrigatória", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ComOpcaoDesconhecida_DeveLancarExcecao()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateDatabaseCommand.ExecuteAsync(["--nao-existe", "1"]));

        Assert.True(
            ex.Message.Contains("desconhecida", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("unknown option", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ComDdlFileInexistente_DeveLancarExcecao()
    {
        string pastaTemp = Path.Combine(Path.GetTempPath(), "SkyFBTool.Tests.CreateDb", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pastaTemp);

        try
        {
            string caminhoBanco = Path.Combine(pastaTemp, "novo.fdb");
            string ddlInexistente = Path.Combine(pastaTemp, "nao_existe.sql");

            var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
                CreateDatabaseCommand.ExecuteAsync([
                    "--database", caminhoBanco,
                    "--overwrite",
                    "--ddl-file", ddlInexistente
                ]));

            Assert.True(
                ex.Message.Contains("não encontrado", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(pastaTemp))
                Directory.Delete(pastaTemp, recursive: true);
        }
    }
}
