namespace SkyFBTool.Services.Import;

public sealed class FalhaImportacaoSqlException : Exception
{
    public FalhaImportacaoSqlException(
        string arquivo,
        long linhaInicioComando,
        string comandoSql,
        Exception innerException)
        : base("Falha ao executar comando SQL durante a importação.", innerException)
    {
        Arquivo = arquivo;
        LinhaInicioComando = linhaInicioComando;
        ComandoSql = comandoSql;
    }

    public string Arquivo { get; }

    public long LinhaInicioComando { get; }

    public string ComandoSql { get; }
}
