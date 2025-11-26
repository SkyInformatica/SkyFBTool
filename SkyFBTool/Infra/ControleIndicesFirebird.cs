using FirebirdSql.Data.FirebirdClient;

namespace SkyFBTool.Infra;

/// <summary>
/// Responsável por desativar e reativar índices de tabelas durante operações de importação em massa.
/// </summary>
public class ControleIndicesFirebird
{
    private readonly FbConnection _conexao;

    // Cache interno para evitar executar INACTIVE duas vezes na mesma tabela
    private readonly HashSet<string> _tabelasComIndicesDesativados =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ControleIndicesFirebird(FbConnection conexao)
    {
        _conexao = conexao ?? throw new ArgumentNullException(nameof(conexao));
    }

    /// <summary>
    /// Desativa todos os índices não únicos de uma tabela.
    /// (Evita custo de atualização de índice durante cargas massivas)
    /// </summary>
    public async Task DesativarIndicesAsync(string tabela, FbTransaction transacao)
    {
        if (string.IsNullOrWhiteSpace(tabela))
            return;

        tabela = tabela.Trim().ToUpperInvariant();

        // Já desativamos antes → não desativar novamente
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

    /// <summary>
    /// Reativa todos os índices das tabelas anteriormente desativadas.
    /// (Rebuild eficiente após importação)
    /// </summary>
    public async Task ReativarTodosAsync(FbTransaction transacao)
    {
        foreach (var tabela in _tabelasComIndicesDesativados)
        {
            await ReativarIndicesAsync(tabela, transacao);
        }

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

    /// <summary>
    /// Busca o nome dos índices da tabela (exceto índices únicos, que não podem ser desativados).
    /// </summary>
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
