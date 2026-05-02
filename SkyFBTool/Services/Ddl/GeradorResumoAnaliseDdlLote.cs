using System.Net;
using System.Text;
using System.Text.Json;
using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl;

public static class GeradorResumoAnaliseDdlLote
{
    public static async Task<(string ArquivoJson, string ArquivoHtml)> GerarAsync(
        IReadOnlyList<EntradaResumoAnaliseDdlLote> entradas,
        string? saidaBase,
        IdiomaSaida idioma)
    {
        if (entradas.Count == 0)
            throw new ArgumentException("Nenhuma entrada de análise foi informada para o resumo em lote.");

        var bases = new List<ItemResumoBaseLote>(entradas.Count);
        var totaisPorCodigo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entrada in entradas)
        {
            string json = await File.ReadAllTextAsync(entrada.ArquivoJson);
            var resultado = JsonSerializer.Deserialize<ResultadoAnaliseDdl>(json, JsonOptions)
                            ?? throw new InvalidDataException($"Arquivo JSON inválido: {entrada.ArquivoJson}");

            foreach (var item in resultado.ResumoPorCodigo)
            {
                totaisPorCodigo.TryGetValue(item.Chave, out int atual);
                totaisPorCodigo[item.Chave] = atual + item.Quantidade;
            }

            bases.Add(new ItemResumoBaseLote
            {
                NomeBase = Path.GetFileNameWithoutExtension(entrada.Banco),
                CaminhoBanco = entrada.Banco,
                ArquivoJson = entrada.ArquivoJson,
                ArquivoHtml = entrada.ArquivoHtml,
                TotalTabelas = resultado.TotalTabelas,
                TotalAchados = resultado.TotalAchados,
                TotalCriticos = resultado.TotalCriticos,
                TotalAltos = resultado.TotalAltos,
                TotalMedios = resultado.TotalMedios,
                TotalBaixos = resultado.TotalBaixos,
                MaiorSeveridade = DeterminarMaiorSeveridade(resultado),
                TopCodigos = resultado.ResumoPorCodigo
                    .OrderByDescending(i => i.Quantidade)
                    .ThenBy(i => i.Chave, StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .Select(i => $"{i.Chave} ({i.Quantidade})")
                    .ToList()
            });
        }

        bases = bases
            .OrderByDescending(b => PesoSeveridade(b.MaiorSeveridade))
            .ThenByDescending(b => b.TotalAchados)
            .ThenBy(b => b.NomeBase, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int totalAchados = bases.Sum(b => b.TotalAchados);
        var resumo = new ResultadoResumoLoteDdl
        {
            GeradoEmUtc = DateTime.UtcNow,
            TotalBases = bases.Count,
            BasesComAchados = bases.Count(b => b.TotalAchados > 0),
            BasesComCriticos = bases.Count(b => b.TotalCriticos > 0),
            TotalTabelas = bases.Sum(b => b.TotalTabelas),
            TotalAchados = totalAchados,
            TotalCriticos = bases.Sum(b => b.TotalCriticos),
            TotalAltos = bases.Sum(b => b.TotalAltos),
            TotalMedios = bases.Sum(b => b.TotalMedios),
            TotalBaixos = bases.Sum(b => b.TotalBaixos),
            Bases = bases,
            ResumoPorCodigo = totaisPorCodigo
                .Select(p => new ItemResumoAnaliseDdl
                {
                    Chave = p.Key,
                    Quantidade = p.Value,
                    Percentual = totalAchados == 0 ? 0 : Math.Round((decimal)p.Value * 100m / totalAchados, 2)
                })
                .OrderByDescending(i => i.Quantidade)
                .ThenBy(i => i.Chave, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        var (arquivoJson, arquivoHtml) = ResolverArquivosSaida(saidaBase);
        Directory.CreateDirectory(Path.GetDirectoryName(arquivoJson)!);

        await File.WriteAllTextAsync(arquivoJson, JsonSerializer.Serialize(resumo, JsonOptions));
        await File.WriteAllTextAsync(arquivoHtml, RenderizarHtml(resumo, idioma));
        return (arquivoJson, arquivoHtml);
    }

    private static string RenderizarHtml(ResultadoResumoLoteDdl resumo, IdiomaSaida idioma)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine($"<title>{Html(M(idioma, "Batch DDL Analysis Summary", "Resumo da Análise DDL em Lote"))}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Segoe UI, Arial, sans-serif; margin: 20px; color: #1f2937; }");
        sb.AppendLine("h1, h2 { margin: 0 0 12px 0; }");
        sb.AppendLine(".meta { margin-bottom: 14px; font-size: 14px; }");
        sb.AppendLine(".kpi { display: flex; gap: 10px; flex-wrap: wrap; margin: 10px 0 18px; }");
        sb.AppendLine(".pill { border: 1px solid #d1d5db; border-radius: 12px; padding: 8px 10px; background: #f9fafb; font-size: 13px; }");
        sb.AppendLine(".card { border: 1px solid #e5e7eb; border-radius: 10px; background: #fff; padding: 10px; margin: 8px 0 16px; }");
        sb.AppendLine(".table-wrap { max-height: 70vh; overflow: auto; border: 1px solid #e5e7eb; border-radius: 8px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; }");
        sb.AppendLine("th, td { border-bottom: 1px solid #eef2f7; padding: 8px; text-align: left; vertical-align: top; font-size: 13px; }");
        sb.AppendLine("th { position: sticky; top: 0; background: #f3f4f6; z-index: 1; }");
        sb.AppendLine(".severity-pill { display: inline-flex; align-items: center; justify-content: center; min-height: 22px; padding: 0 10px; border-radius: 999px; font-size: 12px; font-weight: 700; border: 1px solid; }");
        sb.AppendLine(".severity-pill.critical { color: #7f1d1d; background: #fee2e2; border-color: #fca5a5; }");
        sb.AppendLine(".severity-pill.high { color: #9a3412; background: #ffedd5; border-color: #fdba74; }");
        sb.AppendLine(".severity-pill.medium { color: #1e3a8a; background: #dbeafe; border-color: #93c5fd; }");
        sb.AppendLine(".severity-pill.low { color: #065f46; background: #d1fae5; border-color: #86efac; }");
        sb.AppendLine("@media (max-width: 900px) { body { margin: 12px; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>{Html(M(idioma, "Batch DDL Analysis Summary", "Resumo da Análise DDL em Lote"))}</h1>");
        sb.AppendLine("<div class=\"meta\">");
        sb.AppendLine($"<div><strong>{Html(M(idioma, "Generated at (UTC)", "Gerado em (UTC)"))}:</strong> {resumo.GeradoEmUtc:yyyy-MM-dd HH:mm:ss}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"kpi\">");
        sb.AppendLine($"<div class=\"pill\"><strong>{Html(M(idioma, "Databases", "Bases"))}:</strong> {resumo.TotalBases}</div>");
        sb.AppendLine($"<div class=\"pill\"><strong>{Html(M(idioma, "Databases with findings", "Bases com achados"))}:</strong> {resumo.BasesComAchados}</div>");
        sb.AppendLine($"<div class=\"pill\"><strong>{Html(M(idioma, "Databases with critical findings", "Bases com achados críticos"))}:</strong> {resumo.BasesComCriticos}</div>");
        sb.AppendLine($"<div class=\"pill\"><strong>{Html(M(idioma, "Total findings", "Total de achados"))}:</strong> {resumo.TotalAchados}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine($"<h2>{Html(M(idioma, "Summary per database", "Resumo por base"))}</h2>");
        sb.AppendLine("<div class=\"table-wrap\">");
        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine($"<th>{Html(M(idioma, "Database", "Banco"))}</th>");
        sb.AppendLine(
            $"<th>{Html(SeveridadeRotulo("critical", idioma))}</th>" +
            $"<th>{Html(SeveridadeRotulo("high", idioma))}</th>" +
            $"<th>{Html(SeveridadeRotulo("medium", idioma))}</th>" +
            $"<th>{Html(SeveridadeRotulo("low", idioma))}</th>" +
            $"<th>{Html(M(idioma, "Total", "Total"))}</th>");
        sb.AppendLine($"<th>{Html(M(idioma, "Highest severity", "Maior severidade"))}</th>");
        sb.AppendLine($"<th>{Html(M(idioma, "Top codes", "Top códigos"))}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var item in resumo.Bases)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{Html(item.NomeBase)}</td>");
            sb.AppendLine($"<td>{item.TotalCriticos}</td>");
            sb.AppendLine($"<td>{item.TotalAltos}</td>");
            sb.AppendLine($"<td>{item.TotalMedios}</td>");
            sb.AppendLine($"<td>{item.TotalBaixos}</td>");
            sb.AppendLine($"<td>{item.TotalAchados}</td>");
            sb.AppendLine($"<td><span class='severity-pill {Html(item.MaiorSeveridade)}'>{Html(SeveridadeRotulo(item.MaiorSeveridade, idioma))}</span></td>");
            sb.AppendLine($"<td>{Html(item.TopCodigos.Count == 0 ? "-" : string.Join(", ", item.TopCodigos))}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></div></div>");

        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine($"<h2>{Html(M(idioma, "Top findings by code (all databases)", "Top achados por código (todas as bases)"))}</h2>");
        sb.AppendLine("<div class=\"table-wrap\">");
        sb.AppendLine("<table><thead><tr>");
        sb.AppendLine($"<th>{Html(M(idioma, "Code", "Código"))}</th><th>{Html(M(idioma, "Count", "Quantidade"))}</th><th>%</th>");
        sb.AppendLine("</tr></thead><tbody>");
        if (resumo.ResumoPorCodigo.Count == 0)
        {
            sb.AppendLine($"<tr><td colspan='3'>{Html(M(idioma, "No findings.", "Sem achados."))}</td></tr>");
        }
        else
        {
            foreach (var item in resumo.ResumoPorCodigo.Take(20))
                sb.AppendLine($"<tr><td>{Html(item.Chave)}</td><td>{item.Quantidade}</td><td>{item.Percentual:0.##}</td></tr>");
        }

        sb.AppendLine("</tbody></table></div></div></body></html>");
        return sb.ToString();
    }

    private static (string ArquivoJson, string ArquivoHtml) ResolverArquivosSaida(string? saidaBase)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string padrao = $"batch_analysis_summary_{timestamp}";

        if (string.IsNullOrWhiteSpace(saidaBase))
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), padrao);
            return ($"{basePath}.json", $"{basePath}.html");
        }

        string saida = saidaBase.Trim();
        if (Directory.Exists(saida) || saida.EndsWith(Path.DirectorySeparatorChar) || saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string basePath = Path.Combine(Path.GetFullPath(saida), padrao);
            return ($"{basePath}.json", $"{basePath}.html");
        }

        string semExtensao = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(saida)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(saida) + "_batch_summary");

        return ($"{semExtensao}.json", $"{semExtensao}.html");
    }

    private static string DeterminarMaiorSeveridade(ResultadoAnaliseDdl resultado)
    {
        if (resultado.TotalCriticos > 0) return "critical";
        if (resultado.TotalAltos > 0) return "high";
        if (resultado.TotalMedios > 0) return "medium";
        return "low";
    }

    private static int PesoSeveridade(string severidade)
    {
        return severidade switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            _ => 1
        };
    }

    private static string SeveridadeRotulo(string severidade, IdiomaSaida idioma)
    {
        if (idioma != IdiomaSaida.PortugueseBrazil)
            return severidade.ToLowerInvariant() switch
            {
                "critical" => "Critical",
                "high" => "High",
                "medium" => "Medium",
                "low" => "Low",
                _ => severidade
            };

        return severidade.ToLowerInvariant() switch
        {
            "critical" => "Crítico",
            "high" => "Alto",
            "medium" => "Médio",
            "low" => "Baixo",
            _ => severidade
        };
    }

    private static string Html(string texto)
    {
        return WebUtility.HtmlEncode(texto);
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed class EntradaResumoAnaliseDdlLote
{
    public string Banco { get; set; } = string.Empty;
    public string ArquivoJson { get; set; } = string.Empty;
    public string ArquivoHtml { get; set; } = string.Empty;
}

public sealed class ResultadoResumoLoteDdl
{
    public DateTime GeradoEmUtc { get; set; } = DateTime.UtcNow;
    public int TotalBases { get; set; }
    public int BasesComAchados { get; set; }
    public int BasesComCriticos { get; set; }
    public int TotalTabelas { get; set; }
    public int TotalAchados { get; set; }
    public int TotalCriticos { get; set; }
    public int TotalAltos { get; set; }
    public int TotalMedios { get; set; }
    public int TotalBaixos { get; set; }
    public List<ItemResumoBaseLote> Bases { get; set; } = [];
    public List<ItemResumoAnaliseDdl> ResumoPorCodigo { get; set; } = [];
}

public sealed class ItemResumoBaseLote
{
    public string NomeBase { get; set; } = string.Empty;
    public string CaminhoBanco { get; set; } = string.Empty;
    public string ArquivoJson { get; set; } = string.Empty;
    public string ArquivoHtml { get; set; } = string.Empty;
    public int TotalTabelas { get; set; }
    public int TotalAchados { get; set; }
    public int TotalCriticos { get; set; }
    public int TotalAltos { get; set; }
    public int TotalMedios { get; set; }
    public int TotalBaixos { get; set; }
    public string MaiorSeveridade { get; set; } = "low";
    public List<string> TopCodigos { get; set; } = [];
}
