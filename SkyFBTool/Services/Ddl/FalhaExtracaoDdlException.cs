namespace SkyFBTool.Services.Ddl;

public sealed class FalhaExtracaoDdlException : Exception
{
    public FalhaExtracaoDdlException(string database, Exception innerException)
        : base("Falha ao extrair metadados DDL do banco.", innerException)
    {
        Database = database;
    }

    public string Database { get; }
}
