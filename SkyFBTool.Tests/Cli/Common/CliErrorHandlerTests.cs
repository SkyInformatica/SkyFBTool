using SkyFBTool.Cli.Common;
using SkyFBTool.Services.Import;
using Xunit;

namespace SkyFBTool.Tests.Cli.Common;

public class CliErrorHandlerTests
{
    [Fact]
    public void Exibir_ComFalhaImportacaoSql_DeveExibirContextoDoComando()
    {
        string comandoLongo = "INSERT INTO T (C) VALUES ('" + new string('X', 250) + "')";
        var ex = new FalhaImportacaoSqlException(
            @"C:\dados\entrada.sql",
            42,
            comandoLongo,
            new Exception("erro interno"));

        using var writer = new StringWriter();
        var erroAnterior = Console.Error;
        Console.SetError(writer);

        try
        {
            CliErrorHandler.Exibir(ex);
        }
        finally
        {
            Console.SetError(erroAnterior);
        }

        string saida = writer.ToString();

        Assert.Contains(@"C:\dados\entrada.sql", saida);
        Assert.Contains("42", saida);
        Assert.True(
            saida.Contains("Prévia do comando", StringComparison.OrdinalIgnoreCase) ||
            saida.Contains("Command preview", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("...", saida);
        Assert.True(
            saida.Contains("erro interno", StringComparison.OrdinalIgnoreCase) ||
            saida.Contains("internal", StringComparison.OrdinalIgnoreCase));
    }
}
