using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;

namespace SkyFBTool.Infra;

public static class FabricaConexaoFirebird
{
    public static FbConnection CriarConexao(OpcoesExportacao opcoes)
    {
        var builder = new FbConnectionStringBuilder
        {
            DataSource = opcoes.Host,
            Port = opcoes.Porta,
            Database = opcoes.Database,
            UserID = opcoes.Usuario,
            Password = opcoes.Senha,
            Charset = opcoes.Charset,
            Dialect = 3
        };

        return new FbConnection(builder.ToString());
    }
}