using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Import;

public static class ExecutorSql
{
    public static async Task<(FbTransaction? Transacao, bool HouveErro)> ExecutarAsync(
        string comandoSql,
        FbConnection conexao,
        FbTransaction? transacao,
        OpcoesImportacao opcoes,
        string caminhoLogErros)
    {
        if ((conexao == null) || (transacao == null))
            throw new ArgumentNullException();

        string sql = comandoSql.Trim();

        if (string.IsNullOrWhiteSpace(sql))
            return (transacao, false);

        if (sql.StartsWith("--") || sql.StartsWith("/*"))
            return (transacao, false);

        if (sql.EndsWith(";"))
            sql = sql.Substring(0, sql.Length - 1);

        if (sql.Equals("COMMIT", StringComparison.InvariantCultureIgnoreCase))
        {
            await transacao.CommitAsync();
            await transacao.DisposeAsync();

            transacao = await conexao.BeginTransactionAsync();
            return (transacao, false);
        }

        if (sql.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
        {
            var cmdSemTransacao = new FbCommand(sql, conexao);
            await cmdSemTransacao.ExecuteNonQueryAsync();
            return (transacao, false);
        }

        if (transacao == null)
            transacao = await conexao.BeginTransactionAsync();

        try
        {
            await using var cmd = new FbCommand(sql, conexao, transacao);
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync();
            return (transacao, false);
        }
        catch (Exception ex)
        {
            if (!opcoes.ContinuarEmCasoDeErro)
                throw;

            File.AppendAllText(caminhoLogErros,
                $"Erro ao executar SQL: {sql}{Environment.NewLine}Erro: {ex.Message}{Environment.NewLine}{Environment.NewLine}");
            return (transacao, true);
        }
    }
}
