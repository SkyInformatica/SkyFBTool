using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;

namespace SkyFBTool.Infra;

public static class FabricaConexaoFirebird
{
    public static FbConnection CriarConexao(OpcoesExportacao opcoes)
        => CriarConexao(opcoes.Host, opcoes.Porta, opcoes.Database, opcoes.Usuario, opcoes.Senha, opcoes.Charset);

    public static FbConnection CriarConexao(
        string host,
        int porta,
        string database,
        string usuario,
        string senha,
        string? charset)
    {
        var csb = new FbConnectionStringBuilder
        {
            DataSource = host,
            Port = porta,
            Database = database,
            UserID = usuario,
            Password = senha,
            Dialect = 3,
            Charset = string.IsNullOrWhiteSpace(charset)
                ? "NONE"
                : charset
        };

        return new FbConnection(csb.ConnectionString);
    }
}
