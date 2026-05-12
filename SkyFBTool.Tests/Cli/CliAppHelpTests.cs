using SkyFBTool.Cli;
using Xunit;

namespace SkyFBTool.Tests.Cli;

public class CliAppHelpTests
{
    [Fact]
    public async Task RunAsync_SemArgumentos_DeveExibirResumoDeComandos()
    {
        using var writer = new StringWriter();
        var saidaAnterior = Console.Out;
        Console.SetOut(writer);

        try
        {
            await CliApp.RunAsync([]);
        }
        finally
        {
            Console.SetOut(saidaAnterior);
        }

        string saida = writer.ToString();
        Assert.Contains("COMANDOS", saida, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create-db", saida, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ComandoComHelp_DeveExibirAjudaApenasDoComando()
    {
        using var writer = new StringWriter();
        var saidaAnterior = Console.Out;
        Console.SetOut(writer);

        try
        {
            await CliApp.RunAsync(["export", "--help"]);
        }
        finally
        {
            Console.SetOut(saidaAnterior);
        }

        string saida = writer.ToString();
        Assert.Contains("COMANDO: export", saida, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COMANDO: import", saida, StringComparison.OrdinalIgnoreCase);
    }
}
