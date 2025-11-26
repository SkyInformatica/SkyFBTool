using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Import;

public static class ExecutorSql
{
    public static async Task<FbTransaction?> ExecutarAsync(
        string comandoSql,
        FbConnection conexao,
        FbTransaction? transacao,
        OpcoesImportacao opcoes,
        string caminhoLogErros)
    {
        string sql = comandoSql.Trim();

        // Ignorar linhas vazias
        if (string.IsNullOrWhiteSpace(sql))
            return transacao;

        // Ignorar comentários
        if (sql.StartsWith("--") || sql.StartsWith("/*"))
            return transacao;

        // Remover ";" final
        if (sql.EndsWith(";"))
            sql = sql.Substring(0, sql.Length - 1);

        //
        // 🔵 COMMIT vindo do arquivo
        //
        if (sql.Equals("COMMIT", StringComparison.InvariantCultureIgnoreCase))
        {
            await transacao.CommitAsync();
            await transacao.DisposeAsync();
            
            // criar nova transação
            transacao = await conexao.BeginTransactionAsync();
            return transacao;
        }

        if (sql.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
        {
            // SET NAMES, SET SQL DIALECT, SET AUTODDL, SET IDENTITY_INSERT, etc
            var cmdSemTransacao = new FbCommand(sql, conexao); // sem transação
            await cmdSemTransacao.ExecuteNonQueryAsync();
            return transacao;
        }
        //
        // 🔥 INSERTs precisam de transação ativa
        //
        if (transacao == null)
            transacao = await conexao.BeginTransactionAsync();
        
        //
        // 🔥 DEMAIS COMANDOS (normalmente INSERT)
        //
        try
        {
            await using var cmd = new FbCommand(sql, conexao, transacao);
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // INTERROMPER EXECUÇÃO
            if (!opcoes.ContinuarEmCasoDeErro)
                throw;

            // CONTINUAR E LOGAR
            File.AppendAllText(caminhoLogErros,
                $"Erro ao executar SQL: {sql}{Environment.NewLine}Erro: {ex.Message}{Environment.NewLine}{Environment.NewLine}");
        }
        return transacao;
    }
    
}
