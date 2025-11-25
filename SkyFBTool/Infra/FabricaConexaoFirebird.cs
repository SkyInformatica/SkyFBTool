using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;

namespace SkyFBTool.Infra;

public static class FabricaConexaoFirebird
{
    public static FbConnection CriarConexao(OpcoesExportacao opcoes)
    {
        var csb = new FbConnectionStringBuilder
        {
            DataSource = opcoes.Host,
            Port = opcoes.Porta,
            Database = opcoes.Database,
            UserID = opcoes.Usuario,
            Password = opcoes.Senha,
            Dialect = 3,
            Charset = string.IsNullOrWhiteSpace(opcoes.Charset)
                ? "NONE"
                : opcoes.Charset
        };

        return new FbConnection(csb.ConnectionString);
    }
}