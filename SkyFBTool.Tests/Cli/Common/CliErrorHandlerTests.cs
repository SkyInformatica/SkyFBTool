using SkyFBTool.Cli.Common;
using SkyFBTool.Services.Ddl;
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

    [Fact]
    public void Exibir_ComFalhaExtracaoDdl_DeveClassificarComoFalhaDeAcessoAoArquivo()
    {
        var ex = new FalhaExtracaoDdlException(
            @"C:\dados\origem.fdb",
            new Exception("I/O error for file C:\\dados\\origem.fdb"));

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
        Assert.Contains(@"C:\dados\origem.fdb", saida);
        Assert.True(
            saida.Contains("Categoria da falha: database_file_access", StringComparison.OrdinalIgnoreCase) ||
            saida.Contains("Failure category: database_file_access", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Exibir_ComFalhaExtracaoDdlOds_DeveClassificarComoIncompatibleOds()
    {
        var ex = new FalhaExtracaoDdlException(
            @"D:\dados\auditoria.fdb",
            new Exception("unsupported on-disk structure for file D:\\dados\\auditoria.fdb; found 13.0, support 11.2"));

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
        Assert.True(
            saida.Contains("Categoria da falha: incompatible_ods", StringComparison.OrdinalIgnoreCase) ||
            saida.Contains("Failure category: incompatible_ods", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            saida.Contains("ODS", StringComparison.OrdinalIgnoreCase));
    }
}
