using System.Text.Json;

namespace SkyFBTool.Services.Ddl;

public static class ConfiguracaoSeveridadeDdl
{
    public static async Task<Dictionary<string, string>> CarregarAsync(string caminhoArquivo)
    {
        string caminho = Path.GetFullPath(caminhoArquivo.Trim().Trim('"'));
        if (!File.Exists(caminho))
            throw new FileNotFoundException($"Arquivo de configuracao de severidade nao encontrado: {caminho}");

        string json = await File.ReadAllTextAsync(caminho);
        var documento = JsonSerializer.Deserialize<DocumentoConfiguracao>(json, JsonOptions)
                        ?? new DocumentoConfiguracao();

        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in documento.Overrides)
        {
            string codigoExterno = (item.Code ?? string.Empty).Trim();
            string codigo = NormalizarCodigo(codigoExterno);
            string severidade = NormalizarSeveridade(item.Severity ?? string.Empty);

            if (string.IsNullOrWhiteSpace(codigoExterno))
                throw new ArgumentException("Invalid severity configuration: empty 'code' field.");

            if (string.IsNullOrWhiteSpace(codigo))
            {
                throw new ArgumentException(
                    $"Invalid severity configuration for code '{codigoExterno}'. " +
                    "Use a valid English alias from docs/examples/ddl-severity.sample.json.");
            }

            if (string.IsNullOrWhiteSpace(severidade))
            {
                throw new ArgumentException(
                    $"Invalid severity configuration for code '{codigo}'. " +
                    "Use: critical, high, medium, or low.");
            }

            resultado[codigo] = severidade;
        }

        return resultado;
    }

    public static string AplicarOverride(
        string codigo,
        string severidadePadrao,
        IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null || overrides.Count == 0)
            return severidadePadrao;

        return overrides.TryGetValue(codigo, out var severidade)
            ? severidade
            : severidadePadrao;
    }

    public static string NormalizarSeveridade(string severidade)
    {
        string valor = severidade.Trim().ToLowerInvariant();
        return valor switch
        {
            "critical" => "critical",
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            _ => string.Empty
        };
    }

    public static string NormalizarCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return string.Empty;

        string valor = codigo.Trim().ToUpperInvariant();
        return CodigoAlias.TryGetValue(valor, out var interno) ? interno : string.Empty;
    }

    private sealed class DocumentoConfiguracao
    {
        public List<ItemSeverityOverride> Overrides { get; set; } = [];
    }

    private sealed class ItemSeverityOverride
    {
        public string Code { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Dictionary<string, string> CodigoAlias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TABLE_WITHOUT_PK"] = "TABELA_SEM_PK",
        ["FK_WITHOUT_COVERING_INDEX"] = "FK_SEM_INDICE_COBERTURA",
        ["DUPLICATED_INDEX"] = "INDICE_DUPLICADO",
        ["TABLE_WITHOUT_COLUMNS"] = "TABELA_SEM_COLUNAS",
        ["DUPLICATED_COLUMN"] = "COLUNA_DUPLICADA",
        ["UNKNOWN_TYPE"] = "TIPO_DESCONHECIDO",
        ["PK_WITHOUT_COLUMNS"] = "PK_SEM_COLUNAS",
        ["PK_REFERENCES_MISSING_COLUMN"] = "PK_REFERENCIA_COLUNA_INEXISTENTE",
        ["FK_WITHOUT_COLUMNS"] = "FK_SEM_COLUNAS",
        ["FK_INVALID_CARDINALITY"] = "FK_CARDINALIDADE_INVALIDA",
        ["FK_MISSING_LOCAL_COLUMN"] = "FK_COLUNA_LOCAL_INEXISTENTE",
        ["FK_MISSING_REFERENCED_TABLE"] = "FK_TABELA_REFERENCIA_INEXISTENTE",
        ["FK_MISSING_REFERENCED_COLUMN"] = "FK_COLUNA_REFERENCIA_INEXISTENTE",
        ["INDEX_WITHOUT_COLUMNS"] = "INDICE_SEM_COLUNAS",
        ["INDEX_MISSING_COLUMN"] = "INDICE_COLUNA_INEXISTENTE",
        ["DUPLICATED_FK"] = "FK_DUPLICADA"
    };
}
