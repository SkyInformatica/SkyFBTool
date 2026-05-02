using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Import;

public static class ExecutorSql
{
    private const int MaxTentativasExecucao = 3;

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
            await ExecutarComRetryAsync(sql, conexao, transacao);
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

    private static async Task ExecutarComRetryAsync(string sql, FbConnection conexao, FbTransaction transacao)
    {
        Exception? ultimoErro = null;

        for (int tentativa = 1; tentativa <= MaxTentativasExecucao; tentativa++)
        {
            try
            {
                await using var cmd = new FbCommand(sql, conexao, transacao);
                cmd.CommandTimeout = 0;
                await cmd.ExecuteNonQueryAsync();
                return;
            }
            catch (Exception ex) when (EhFalhaTransienteExecucao(ex) && tentativa < MaxTentativasExecucao)
            {
                ultimoErro = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(150 * tentativa));
            }
            catch (Exception ex)
            {
                ultimoErro = ex;
                break;
            }
        }

        throw ultimoErro ?? new InvalidOperationException("Falha ao executar comando SQL.");
    }

    internal static bool EhFalhaTransienteExecucao(Exception ex)
    {
        if (ex is TimeoutException || ex is IOException)
            return true;

        if (ex is FbException fbEx && EhFalhaTransienteFirebird(fbEx))
            return true;

        string mensagem = ex.Message?.ToLowerInvariant() ?? string.Empty;
        return mensagem.Contains("deadlock") ||
               mensagem.Contains("lock conflict") ||
               mensagem.Contains("update conflicts with concurrent update") ||
               mensagem.Contains("connection lost") ||
               mensagem.Contains("read error") ||
               mensagem.Contains("write error") ||
               mensagem.Contains("timeout");
    }

    private static bool EhFalhaTransienteFirebird(FbException ex)
    {
        // Common transient SQLCODE/ISC patterns:
        // -913 (deadlock/update conflict), -902 (I/O/network), -901 (request/network issues)
        return ex.ErrorCode is -913 or -902 or -901;
    }
}
