using System.Text.Json;

namespace SkyFBTool.Services.Ddl;

public static class CarregadorSnapshotSchema
{
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
            throw new FileNotFoundException($"Arquivo de schema nao encontrado: {arquivoJson}");

        string texto = await File.ReadAllTextAsync(arquivoJson);
        var snapshot = JsonSerializer.Deserialize<SnapshotSchema>(texto, JsonOptions);
        if (snapshot is null)
            throw new ArgumentException($"Nao foi possivel ler snapshot JSON: {arquivoJson}");

        return snapshot;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
