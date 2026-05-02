using Scriban;

namespace SkyFBTool.Services.Ddl;

public static class RenderizadorHtmlDiffDdl
{
    public static string Renderizar(
        ResultadoDiffSchema resultado,
        string origemJson,
        string alvoJson,
        IdiomaSaida idioma)
    {
        var modelo = new ModeloRelatorio
        {
            Lang = idioma == IdiomaSaida.PortugueseBrazil ? "pt-BR" : "en",
            Titulo = M(idioma, "DDL Diff Report", "Relatorio DDL Diff"),
            OrigemLabel = M(idioma, "Source", "Origem"),
            AlvoLabel = M(idioma, "Target", "Alvo"),
            OrigemArquivo = H(Path.GetFileName(origemJson)),
            AlvoArquivo = H(Path.GetFileName(alvoJson)),
            GeradoEmLabel = M(idioma, "Generated at (UTC)", "Gerado em (UTC)"),
            GeradoEm = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            ComandosLabel = M(idioma, "Generated SQL commands", "Comandos SQL gerados"),
            CriadosLabel = M(idioma, "Created items", "Itens criados"),
            AlteradosLabel = M(idioma, "Changed items", "Itens alterados"),
            SomenteAlvoLabel = M(idioma, "Items only in target", "Itens somente no alvo"),
            AvisosLabel = M(idioma, "Warnings", "Avisos"),
            ItemLabel = M(idioma, "Item", "Item"),
            NenhumLabel = M(idioma, "None", "Nenhum"),
            FiltrosLabel = M(idioma, "Filters", "Filtros"),
            SecaoLabel = M(idioma, "Section", "Seção"),
            BuscaLabel = M(idioma, "Search", "Busca"),
            BuscaPlaceholder = M(idioma, "Type to search...", "Digite para buscar..."),
            TodosLabel = M(idioma, "All", "Todos"),
            MostrandoLabel = M(idioma, "Showing", "Mostrando"),
            DeLabel = M(idioma, "of", "de"),
            ResumoLabel = M(idioma, "Summary", "Resumo"),
            QuantidadeLabel = M(idioma, "Count", "Quantidade"),
            AcoesLabel = M(idioma, "Actions", "Ações"),
            CopiarLinhaLabel = M(idioma, "Copy line", "Copiar linha"),
            CopiarTodosLabel = M(idioma, "Copy all commands", "Copiar todos os comandos"),
            BaixarSqlLabel = M(idioma, "Download SQL", "Baixar SQL"),
            NomeArquivoSql = "ddl-diff-commands.sql",
            CopiaSucessoLabel = M(idioma, "Copied.", "Copiado."),
            CopiaFalhaLabel = M(idioma, "Copy failed.", "Falha ao copiar."),
            TotalComandos = resultado.ComandosSql.Count,
            TotalCriados = resultado.ItensCriados.Count,
            TotalAlterados = resultado.ItensAlterados.Count,
            TotalSomenteAlvo = resultado.ItensSomenteNoAlvo.Count,
            TotalAvisos = resultado.Avisos.Count,
            Comandos = resultado.ComandosSql.Select(H).ToList(),
            Criados = resultado.ItensCriados.Select(H).ToList(),
            Alterados = resultado.ItensAlterados.Select(H).ToList(),
            SomenteAlvo = resultado.ItensSomenteNoAlvo.Select(H).ToList(),
            Avisos = resultado.Avisos.Select(H).ToList()
        };

        return TemplateHtml.Render(modelo, m => m.Name);
    }

    private static string H(string valor)
    {
        return System.Net.WebUtility.HtmlEncode(valor ?? string.Empty);
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }

    private static Template CriarTemplate()
    {
        using var stream = typeof(RenderizadorHtmlDiffDdl).Assembly
            .GetManifestResourceStream("SkyFBTool.Services.Ddl.Templates.DdlDiffReport.scriban");
        if (stream is null)
            throw new InvalidOperationException("Template de relatorio DDL Diff nao encontrado.");

        using var reader = new StreamReader(stream);
        string conteudo = reader.ReadToEnd();
        var template = Template.Parse(conteudo);
        if (template.HasErrors)
            throw new InvalidOperationException("Template de relatorio DDL Diff invalido.");

        return template;
    }

    private static readonly Template TemplateHtml = CriarTemplate();

    private sealed class ModeloRelatorio
    {
        public string Lang { get; init; } = "en";
        public string Titulo { get; init; } = string.Empty;
        public string OrigemLabel { get; init; } = string.Empty;
        public string AlvoLabel { get; init; } = string.Empty;
        public string OrigemArquivo { get; init; } = string.Empty;
        public string AlvoArquivo { get; init; } = string.Empty;
        public string GeradoEmLabel { get; init; } = string.Empty;
        public string GeradoEm { get; init; } = string.Empty;
        public string ComandosLabel { get; init; } = string.Empty;
        public string CriadosLabel { get; init; } = string.Empty;
        public string AlteradosLabel { get; init; } = string.Empty;
        public string SomenteAlvoLabel { get; init; } = string.Empty;
        public string AvisosLabel { get; init; } = string.Empty;
        public string ItemLabel { get; init; } = string.Empty;
        public string NenhumLabel { get; init; } = string.Empty;
        public string FiltrosLabel { get; init; } = string.Empty;
        public string SecaoLabel { get; init; } = string.Empty;
        public string BuscaLabel { get; init; } = string.Empty;
        public string BuscaPlaceholder { get; init; } = string.Empty;
        public string TodosLabel { get; init; } = string.Empty;
        public string MostrandoLabel { get; init; } = string.Empty;
        public string DeLabel { get; init; } = string.Empty;
        public string ResumoLabel { get; init; } = string.Empty;
        public string QuantidadeLabel { get; init; } = string.Empty;
        public string AcoesLabel { get; init; } = string.Empty;
        public string CopiarLinhaLabel { get; init; } = string.Empty;
        public string CopiarTodosLabel { get; init; } = string.Empty;
        public string BaixarSqlLabel { get; init; } = string.Empty;
        public string NomeArquivoSql { get; init; } = "ddl-diff-commands.sql";
        public string CopiaSucessoLabel { get; init; } = string.Empty;
        public string CopiaFalhaLabel { get; init; } = string.Empty;
        public int TotalComandos { get; init; }
        public int TotalCriados { get; init; }
        public int TotalAlterados { get; init; }
        public int TotalSomenteAlvo { get; init; }
        public int TotalAvisos { get; init; }
        public List<string> Comandos { get; init; } = [];
        public List<string> Criados { get; init; } = [];
        public List<string> Alterados { get; init; } = [];
        public List<string> SomenteAlvo { get; init; } = [];
        public List<string> Avisos { get; init; } = [];
    }
}
