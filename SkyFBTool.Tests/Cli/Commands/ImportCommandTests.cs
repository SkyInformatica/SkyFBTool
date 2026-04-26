using SkyFBTool.Cli.Commands;
using Xunit;

namespace SkyFBTool.Tests.Cli.Commands;

public class ImportCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ComOpcaoDesconhecida_DeveLancarExcecao()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ImportCommand.ExecuteAsync(["--nao-existe", "1"]));

        Assert.True(
            ex.Message.Contains("desconhecida", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("unknown option", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ComInputEInputsBatch_Juntos_DeveLancarExcecao()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ImportCommand.ExecuteAsync(
            [
                "--database", "qualquer.fdb",
                "--input", "arquivo.sql",
                "--inputs-batch", @"C:\tmp\*.sql"
            ]));

        Assert.True(
            ex.Message.Contains("apenas um modo", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("only one input mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ComInputsBatch_SemArquivos_DeveLancarFileNotFound()
    {
        string pastaTemp = Path.Combine(Path.GetTempPath(), "SkyFBTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pastaTemp);

        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                ImportCommand.ExecuteAsync(
                [
                    "--database", "qualquer.fdb",
                    "--inputs-batch", Path.Combine(pastaTemp, "*.sql")
                ]));
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ComAliasInputBatch_DiretorioInexistente_DeveLancarDirectoryNotFound()
    {
        string pastaInexistente = Path.Combine(
            Path.GetTempPath(),
            "SkyFBTool.Tests",
            Guid.NewGuid().ToString("N"),
            "nao_existe");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            ImportCommand.ExecuteAsync(
            [
                "--database", "qualquer.fdb",
                "--input-batch", Path.Combine(pastaInexistente, "*.sql")
            ]));
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
