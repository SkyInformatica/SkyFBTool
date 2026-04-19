using FirebirdSql.Data.FirebirdClient;

namespace SkyFBTool.Infra;

public class ControleIndicesFirebird
{
    private readonly FbConnection _conexao;
    private readonly HashSet<string> _tabelasComIndicesDesativados =
        new(StringComparer.OrdinalIgnoreCase);

    public ControleIndicesFirebird(FbConnection conexao)
    {
        _conexao = conexao ?? throw new ArgumentNullException(nameof(conexao));
    }

    public async Task DesativarIndicesAsync(string tabela, FbTransaction transacao)
    {
        if (string.IsNullOrWhiteSpace(tabela))
            return;

        tabela = tabela.Trim().ToUpperInvariant();

        if (_tabelasComIndicesDesativados.Contains(tabela))
            return;

        var indices = await ObterIndicesDaTabelaAsync(tabela, transacao);

        foreach (var indice in indices)
        {
            string sql = $"ALTER INDEX {indice} INACTIVE";
            using var cmd = new FbCommand(sql, _conexao, transacao);
            await cmd.ExecuteNonQueryAsync();
        }

        _tabelasComIndicesDesativados.Add(tabela);
    }

    public async Task ReativarTodosAsync(FbTransaction transacao)
    {
        foreach (var tabela in _tabelasComIndicesDesativados)
            await ReativarIndicesAsync(tabela, transacao);

        _tabelasComIndicesDesativados.Clear();
    }

    private async Task ReativarIndicesAsync(string tabela, FbTransaction transacao)
    {
        var indices = await ObterIndicesDaTabelaAsync(tabela, transacao);

        foreach (var indice in indices)
        {
            string sql = $"ALTER INDEX {indice} ACTIVE";
            using var cmd = new FbCommand(sql, _conexao, transacao);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<string>> ObterIndicesDaTabelaAsync(string tabela, FbTransaction transacao)
    {
        const string sql = @"SELECT * FROM RDB$INDICES 
         WHERE RDB$RELATION_NAME = @TABELA AND RDB$UNIQUE_FLAG = 0 AND RDB$SYSTEM_FLAG = 0";

        using var cmd = new FbCommand(sql, _conexao, transacao);
        cmd.Parameters.AddWithValue("TABELA", tabela);

        var lista = new List<string>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(0).Trim();
            lista.Add(nome);
        }

        return lista;
    }
}
