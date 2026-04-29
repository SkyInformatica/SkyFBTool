using Scriban;

namespace SkyFBTool.Services.Ddl;

public static class RenderizadorHtmlAnaliseDdl
{
    public static string Renderizar(ResultadoAnaliseDdl resultado, IdiomaSaida idioma)
    {
        var modelo = CriarModelo(resultado, idioma);
        return TemplateHtml.Render(modelo, member => member.Name);
    }

    private static ModeloRelatorio CriarModelo(ResultadoAnaliseDdl resultado, IdiomaSaida idioma)
    {
        string origemExibicao = Path.GetFileName(resultado.Origem);
        if (string.IsNullOrWhiteSpace(origemExibicao))
            origemExibicao = resultado.Origem;

        var resumoCodigoTop = resultado.ResumoPorCodigo
            .Take(10)
            .Select(MapearResumo)
            .ToList();
        var resumoTabelaRiscoTop = CriarResumoRiscoPorTabela(resultado);

        return new ModeloRelatorio
        {
            Lang = idioma == IdiomaSaida.PortugueseBrazil ? "pt-BR" : "en",
            Titulo = M(idioma, "DDL Risk Analysis", "Análise de Risco DDL"),
            OrigemLabel = M(idioma, "Source", "Origem"),
            OrigemExibicao = H(origemExibicao),
            OrigemTooltip = H(resultado.Origem),
            DescriptionLabel = M(idioma, "Description", "Descrição"),
            Description = H(resultado.Description),
            HasDescription = !string.IsNullOrWhiteSpace(resultado.Description),
            GeradoEmLabel = M(idioma, "Generated at (UTC)", "Gerado em (UTC)"),
            GeradoEm = resultado.GeradoEmUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            UltimaManutencaoLabel = M(idioma, "Last maintenance (estimated, UTC)", "Última manutenção (estimada, UTC)"),
            UltimaManutencao = resultado.DataUltimaManutencaoUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            FonteUltimaManutencao = H(resultado.FonteDataUltimaManutencao),
            TemUltimaManutencao = resultado.DataUltimaManutencaoUtc is not null,
            AnaliseVolumeLabel = M(idioma, "Volume analysis", "Análise de volume"),
            AnaliseVolumeResumo = H(FormatarResumoAnaliseVolume(resultado, idioma)),
            TotalLabel = M(idioma, "Total findings", "Total de achados"),
            CriticosLabel = M(idioma, "Critical", "Críticos"),
            AltosLabel = M(idioma, "High", "Altos"),
            MediosLabel = M(idioma, "Medium", "Médios"),
            BaixosLabel = M(idioma, "Low", "Baixos"),
            TotalAchados = resultado.TotalAchados,
            TotalCriticos = resultado.TotalCriticos,
            TotalAltos = resultado.TotalAltos,
            TotalMedios = resultado.TotalMedios,
            TotalBaixos = resultado.TotalBaixos,
            ResumoCodigoLabel = M(idioma, "Summary by finding type", "Resumo por tipo de achado"),
            ResumoTabelaLabel = M(idioma, "Tables prioritized for remediation", "Tabelas priorizadas para correção"),
            QtdLabel = M(idioma, "Count", "Qtde"),
            PctLabel = "%",
            CodigoLabel = M(idioma, "Code", "Código"),
            PrioridadeLabel = M(idioma, "Priority", "Prioridade"),
            ScoreRiscoLabel = M(idioma, "Risk score", "Score de risco"),
            IndiceRiscoLabel = M(idioma, "Risk index", "Índice de risco"),
            EscopoLabel = M(idioma, "Scope", "Escopo"),
            DescricaoLabel = M(idioma, "Description", "Descrição"),
            RecomendacaoLabel = M(idioma, "Recommendation", "Recomendação"),
            SemAchadosLabel = M(idioma, "No findings.", "Sem achados."),
            FiltrosLabel = M(idioma, "Filters", "Filtros"),
            SeveridadeLabel = M(idioma, "Severity", "Severidade"),
            CriteriosSeveridadeLabel = M(idioma, "Severity criteria", "Critérios de severidade"),
            CriterioNivelLabel = M(idioma, "Level", "Nível"),
            CriterioQuandoLabel = M(idioma, "When it applies", "Quando se aplica"),
            CriterioImpactoLabel = M(idioma, "Expected impact", "Impacto esperado"),
            TodosLabel = M(idioma, "All", "Todos"),
            BuscaLabel = M(idioma, "Search", "Busca"),
            BuscaPlaceholder = M(idioma, "table, code or text...", "tabela, código ou texto..."),
            MostrandoLabel = M(idioma, "Showing", "Exibindo"),
            DeLabel = M(idioma, "of", "de"),
            MostrandoJs = Js(M(idioma, "Showing", "Exibindo")),
            DeJs = Js(M(idioma, "of", "de")),
            PossuiAchados = resultado.Achados.Count > 0,
            CodigosFiltro = resultado.ResumoPorCodigo.Select(r => H(r.Chave)).ToList(),
            Severidades = CriarSeveridades(idioma),
            CriteriosSeveridade = CriarCriteriosSeveridade(idioma),
            ResumoCodigoTop = resumoCodigoTop,
            ResumoTabelaRiscoTop = resumoTabelaRiscoTop,
            Achados = resultado.Achados.Select(a => new AchadoModelo
            {
                SeveridadeValor = a.Severidade,
                SeveridadeRotulo = H(SeveridadeRotulo(a.Severidade, idioma)),
                Prioridade = H(a.Prioridade),
                ScoreRisco = a.ScoreRisco,
                Codigo = H(a.Codigo),
                Escopo = H(a.Escopo),
                Descricao = H(a.Descricao),
                Recomendacao = H(a.Recomendacao),
                TextoFiltro = H((a.Escopo + " " + a.Codigo + " " + a.Descricao + " " + a.Recomendacao).ToLowerInvariant())
            }).ToList()
        };
    }

    private static List<SeveridadeModelo> CriarSeveridades(IdiomaSaida idioma)
    {
        return
        [
            new() { Valor = "critical", Rotulo = H(SeveridadeRotulo("critical", idioma)) },
            new() { Valor = "high", Rotulo = H(SeveridadeRotulo("high", idioma)) },
            new() { Valor = "medium", Rotulo = H(SeveridadeRotulo("medium", idioma)) },
            new() { Valor = "low", Rotulo = H(SeveridadeRotulo("low", idioma)) }
        ];
    }

    private static List<CriterioSeveridadeModelo> CriarCriteriosSeveridade(IdiomaSaida idioma)
    {
        return
        [
            new()
            {
                Classe = "critical",
                Rotulo = H(SeveridadeRotulo("critical", idioma)),
                Quando = H(M(
                    idioma,
                    "Broken structural metadata: missing or invalid columns in PK/FK, empty constraints, or impossible references.",
                    "Metadado estrutural quebrado: colunas ausentes ou inválidas em PK/FK, constraints vazias ou referências impossíveis.")),
                Impacto = H(M(
                    idioma,
                    "High chance of functional failure, corruption symptoms, or impossible safe sync without manual repair.",
                    "Alta chance de falha funcional, sintomas de corrupção ou impossibilidade de sincronismo seguro sem reparo manual."))
            },
            new()
            {
                Classe = "high",
                Rotulo = H(SeveridadeRotulo("high", idioma)),
                Quando = H(M(
                    idioma,
                    "High-risk inconsistency that can break integrity or performance under load.",
                    "Inconsistência de alto risco que pode quebrar integridade ou performance em carga.")),
                Impacto = H(M(
                    idioma,
                    "Relevant production risk; fix before migration or intensive import/export.",
                    "Risco relevante em produção; corrigir antes de migração ou importação/exportação intensa."))
            },
            new()
            {
                Classe = "medium",
                Rotulo = H(SeveridadeRotulo("medium", idioma)),
                Quando = H(M(
                    idioma,
                    "Important quality or performance gap, usually without immediate corruption.",
                    "Lacuna importante de qualidade ou performance, normalmente sem corrupção imediata.")),
                Impacto = H(M(
                    idioma,
                    "Can degrade throughput and increase lock/contention risk over time.",
                    "Pode degradar vazão e aumentar risco de lock/contenção ao longo do tempo."))
            },
            new()
            {
                Classe = "low",
                Rotulo = H(SeveridadeRotulo("low", idioma)),
                Quando = H(M(
                    idioma,
                    "Advisory pattern or modeling smell with lower immediate risk.",
                    "Padrão consultivo ou smell de modelagem com menor risco imediato.")),
                Impacto = H(M(
                    idioma,
                    "Recommended for hardening and maintainability; can be planned.",
                    "Recomendado para endurecimento e manutenibilidade; pode ser planejado."))
            }
        ];
    }

    private static ResumoModelo MapearResumo(ItemResumoAnaliseDdl item)
    {
        return new ResumoModelo
        {
            Chave = H(item.Chave),
            Quantidade = item.Quantidade,
            Percentual = item.Percentual.ToString("0.##")
        };
    }

    private static List<ResumoTabelaRiscoModelo> CriarResumoRiscoPorTabela(ResultadoAnaliseDdl resultado)
    {
        return resultado.Achados
            .GroupBy(a => NomeTabelaDoEscopo(a.Escopo), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                int quantidade = g.Count();
                int maxScore = g.Max(a => a.ScoreRisco);
                double mediaScore = g.Average(a => a.ScoreRisco);
                int bonusQuantidade = Math.Min(10, quantidade);
                int indiceRisco = Math.Min(100, (int)Math.Round((maxScore * 0.5) + (mediaScore * 0.4) + bonusQuantidade));
                return new ResumoTabelaRiscoModelo
                {
                    Tabela = H(g.Key),
                    Quantidade = quantidade,
                    IndiceRisco = indiceRisco,
                    Prioridade = H(CalcularPrioridade(indiceRisco))
                };
            })
            .OrderByDescending(i => i.IndiceRisco)
            .ThenByDescending(i => i.Quantidade)
            .ThenBy(i => i.Tabela, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NomeTabelaDoEscopo(string escopo)
    {
        if (string.IsNullOrWhiteSpace(escopo))
            return "?";

        int idx = escopo.IndexOf('.');
        return idx < 0 ? escopo : escopo[..idx];
    }

    private static string CalcularPrioridade(int scoreRisco)
    {
        if (scoreRisco >= 85) return "P0";
        if (scoreRisco >= 70) return "P1";
        if (scoreRisco >= 45) return "P2";
        return "P3";
    }

    private static string SeveridadeRotulo(string severidade, IdiomaSaida idioma)
    {
        if (idioma != IdiomaSaida.PortugueseBrazil)
            return severidade;

        return severidade switch
        {
            "critical" => "crítico",
            "high" => "alto",
            "medium" => "médio",
            "low" => "baixo",
            _ => severidade
        };
    }

    private static string H(string valor)
    {
        return System.Net.WebUtility.HtmlEncode(valor ?? string.Empty);
    }

    private static string Js(string valor)
    {
        return valor.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private static string M(IdiomaSaida idioma, string english, string portuguese)
    {
        return idioma == IdiomaSaida.PortugueseBrazil ? portuguese : english;
    }

    private static string FormatarStatusAnaliseVolume(ResultadoAnaliseDdl resultado, IdiomaSaida idioma)
    {
        return resultado.StatusAnaliseVolume switch
        {
            "executed" => M(idioma, $"executed ({resultado.TabelasLidasAnaliseVolume} tables, {resultado.AchadosGeradosAnaliseVolume} findings)", $"executada ({resultado.TabelasLidasAnaliseVolume} tabelas, {resultado.AchadosGeradosAnaliseVolume} achados)"),
            "disabled" => M(idioma, "disabled", "desabilitada"),
            "failed" => M(idioma, "failed", "falhou"),
            "pending" => M(idioma, "pending", "pendente"),
            _ => M(idioma, "not applicable", "não aplicável")
        };
    }

    private static string FormatarResumoAnaliseVolume(ResultadoAnaliseDdl resultado, IdiomaSaida idioma)
    {
        string status = FormatarStatusAnaliseVolume(resultado, idioma);
        if (resultado.StatusAnaliseVolume == "failed" && !string.IsNullOrWhiteSpace(resultado.ErroAnaliseVolume))
            return $"{status}. {resultado.ErroAnaliseVolume}";

        return status;
    }

    private static Template CriarTemplate()
    {
        using var stream = typeof(RenderizadorHtmlAnaliseDdl).Assembly
            .GetManifestResourceStream("SkyFBTool.Services.Ddl.Templates.DdlAnalyzeReport.scriban");
        if (stream is null)
            throw new InvalidOperationException("Template de relatório DDL não encontrado.");

        using var reader = new StreamReader(stream);
        string conteudo = reader.ReadToEnd();
        var template = Template.Parse(conteudo);
        if (template.HasErrors)
            throw new InvalidOperationException("Template de relatório DDL inválido.");

        return template;
    }

    private static readonly Template TemplateHtml = CriarTemplate();

    private sealed class ModeloRelatorio
    {
        public string Lang { get; init; } = "en";
        public string Titulo { get; init; } = string.Empty;
        public string OrigemLabel { get; init; } = string.Empty;
        public string OrigemExibicao { get; init; } = string.Empty;
        public string OrigemTooltip { get; init; } = string.Empty;
        public string DescriptionLabel { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool HasDescription { get; init; }
        public string GeradoEmLabel { get; init; } = string.Empty;
        public string GeradoEm { get; init; } = string.Empty;
        public string UltimaManutencaoLabel { get; init; } = string.Empty;
        public string UltimaManutencao { get; init; } = string.Empty;
        public string FonteUltimaManutencao { get; init; } = string.Empty;
        public bool TemUltimaManutencao { get; init; }
        public string AnaliseVolumeLabel { get; init; } = string.Empty;
        public string AnaliseVolumeResumo { get; init; } = string.Empty;
        public string TotalLabel { get; init; } = string.Empty;
        public string CriticosLabel { get; init; } = string.Empty;
        public string AltosLabel { get; init; } = string.Empty;
        public string MediosLabel { get; init; } = string.Empty;
        public string BaixosLabel { get; init; } = string.Empty;
        public int TotalAchados { get; init; }
        public int TotalCriticos { get; init; }
        public int TotalAltos { get; init; }
        public int TotalMedios { get; init; }
        public int TotalBaixos { get; init; }
        public string ResumoCodigoLabel { get; init; } = string.Empty;
        public string ResumoTabelaLabel { get; init; } = string.Empty;
        public string QtdLabel { get; init; } = string.Empty;
        public string PctLabel { get; init; } = "%";
        public string CodigoLabel { get; init; } = string.Empty;
        public string PrioridadeLabel { get; init; } = string.Empty;
        public string ScoreRiscoLabel { get; init; } = string.Empty;
        public string IndiceRiscoLabel { get; init; } = string.Empty;
        public string EscopoLabel { get; init; } = string.Empty;
        public string DescricaoLabel { get; init; } = string.Empty;
        public string RecomendacaoLabel { get; init; } = string.Empty;
        public string SemAchadosLabel { get; init; } = string.Empty;
        public string FiltrosLabel { get; init; } = string.Empty;
        public string SeveridadeLabel { get; init; } = string.Empty;
        public string CriteriosSeveridadeLabel { get; init; } = string.Empty;
        public string CriterioNivelLabel { get; init; } = string.Empty;
        public string CriterioQuandoLabel { get; init; } = string.Empty;
        public string CriterioImpactoLabel { get; init; } = string.Empty;
        public string TodosLabel { get; init; } = string.Empty;
        public string BuscaLabel { get; init; } = string.Empty;
        public string BuscaPlaceholder { get; init; } = string.Empty;
        public string MostrandoLabel { get; init; } = string.Empty;
        public string DeLabel { get; init; } = string.Empty;
        public string MostrandoJs { get; init; } = string.Empty;
        public string DeJs { get; init; } = string.Empty;
        public bool PossuiAchados { get; init; }
        public List<string> CodigosFiltro { get; init; } = [];
        public List<SeveridadeModelo> Severidades { get; init; } = [];
        public List<CriterioSeveridadeModelo> CriteriosSeveridade { get; init; } = [];
        public List<ResumoModelo> ResumoCodigoTop { get; init; } = [];
        public List<ResumoTabelaRiscoModelo> ResumoTabelaRiscoTop { get; init; } = [];
        public List<AchadoModelo> Achados { get; init; } = [];
    }

    private sealed class ResumoModelo
    {
        public string Chave { get; init; } = string.Empty;
        public int Quantidade { get; init; }
        public string Percentual { get; init; } = "0";
    }

    private sealed class SeveridadeModelo
    {
        public string Valor { get; init; } = string.Empty;
        public string Rotulo { get; init; } = string.Empty;
    }

    private sealed class ResumoTabelaRiscoModelo
    {
        public string Tabela { get; init; } = string.Empty;
        public int Quantidade { get; init; }
        public int IndiceRisco { get; init; }
        public string Prioridade { get; init; } = string.Empty;
    }

    private sealed class CriterioSeveridadeModelo
    {
        public string Classe { get; init; } = string.Empty;
        public string Rotulo { get; init; } = string.Empty;
        public string Quando { get; init; } = string.Empty;
        public string Impacto { get; init; } = string.Empty;
    }

    private sealed class AchadoModelo
    {
        public string SeveridadeValor { get; init; } = string.Empty;
        public string SeveridadeRotulo { get; init; } = string.Empty;
        public string Prioridade { get; init; } = string.Empty;
        public int ScoreRisco { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string Escopo { get; init; } = string.Empty;
        public string Descricao { get; init; } = string.Empty;
        public string Recomendacao { get; init; } = string.Empty;
        public string TextoFiltro { get; init; } = string.Empty;
    }
}
