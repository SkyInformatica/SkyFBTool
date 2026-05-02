using System.Text.Json;

namespace SkyFBTool.Services.Ddl;

public static class CarregadorSnapshotSchema
{
    public static async Task<(SnapshotSchema Snapshot, string Origem)> CarregarSnapshotComOrigemAsync(string caminhoInformado)
    {
        string caminho = Path.GetFullPath(caminhoInformado.Trim().Trim('"'));
        string extensao = Path.GetExtension(caminho);

        if (extensao.Equals(".json", StringComparison.OrdinalIgnoreCase))
            return (await LerArquivoJsonAsync(caminho), caminho);

        if (extensao.Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            string arquivoJson = Path.ChangeExtension(caminho, ".schema.json");
            if (File.Exists(arquivoJson))
                return (await LerArquivoJsonAsync(arquivoJson), arquivoJson);

            if (!File.Exists(caminho))
                throw new FileNotFoundException($"Arquivo SQL não encontrado: {caminho}");

            return (await ParserSqlDdlSnapshot.LerArquivoSqlAsync(caminho), caminho);
        }

        string candidatoJson = $"{caminho}.schema.json";
        if (File.Exists(candidatoJson))
            return (await LerArquivoJsonAsync(candidatoJson), candidatoJson);

        string candidatoSql = $"{caminho}.sql";
        if (File.Exists(candidatoSql))
            return (await ParserSqlDdlSnapshot.LerArquivoSqlAsync(candidatoSql), candidatoSql);

        throw new FileNotFoundException(
            $"Não foi encontrado arquivo de schema para '{caminhoInformado}'. " +
            $"Esperado: '{candidatoJson}' ou '{candidatoSql}'.");
    }

    public static string ResolverArquivoJsonSchema(string caminhoInformado)
    {
        string caminho = Path.GetFullPath(caminhoInformado.Trim().Trim('"'));

        if (Path.GetExtension(caminho).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return caminho;

        if (Path.GetExtension(caminho).Equals(".sql", StringComparison.OrdinalIgnoreCase))
            return Path.ChangeExtension(caminho, ".schema.json");

        return $"{caminho}.schema.json";
    }

    public static async Task<SnapshotSchema> LerArquivoJsonAsync(string arquivoJson)
    {
        if (!File.Exists(arquivoJson))
            throw new FileNotFoundException($"Arquivo de schema não encontrado: {arquivoJson}");

        string texto = await File.ReadAllTextAsync(arquivoJson);
        var snapshot = JsonSerializer.Deserialize<SnapshotSchema>(texto, JsonOptions);
        if (snapshot is null)
            throw new ArgumentException($"Não foi possível ler snapshot JSON: {arquivoJson}");

        return snapshot;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
