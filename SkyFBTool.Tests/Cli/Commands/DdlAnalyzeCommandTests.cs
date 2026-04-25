using SkyFBTool.Cli.Commands;
using Xunit;

namespace SkyFBTool.Tests.Cli.Commands;

public class DdlAnalyzeCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ComOpcaoDesconhecida_DeveLancarExcecao()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DdlAnalyzeCommand.ExecuteAsync(["--nao-existe", "1"]));

        Assert.True(
            ex.Message.Contains("desconhecida", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("unknown option", StringComparison.OrdinalIgnoreCase));
    }
}
