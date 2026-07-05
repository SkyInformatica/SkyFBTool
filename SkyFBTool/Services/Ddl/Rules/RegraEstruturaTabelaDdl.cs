using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class RegraEstruturaTabelaDdl : IRegraAnaliseDdl
{
    public void Avaliar(ContextoAnaliseDdl contexto)
    {
        foreach (var tabela in contexto.TabelasVisiveis.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            ValidarTabelaSemColunas(tabela, contexto);
            ValidarColunasDuplicadas(tabela, contexto);
            ValidarTiposDesconhecidos(tabela, contexto);
            ValidarPk(tabela, contexto);
        }
    }

    private static void ValidarTabelaSemColunas(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        if (tabela.Colunas.Count > 0)
            return;

        contexto.AdicionarAchado(
            "critical",
            "TABELA_SEM_COLUNAS",
            tabela.Nome,
            TextoLocalizado.Obter(
                contexto.Idioma,
                $"Table {tabela.Nome} has no columns.",
                $"Tabela {tabela.Nome} não possui colunas."),
            TextoLocalizado.Obter(
                contexto.Idioma,
                "Re-extract metadata and validate this table directly in system catalogs.",
                "Reextraia o metadado e valide esta tabela diretamente nos catálogos do Firebird."));
    }

    private static void ValidarColunasDuplicadas(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        var duplicadas = tabela.Colunas
            .GroupBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var coluna in duplicadas.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            contexto.AdicionarAchado(
                "critical",
                "COLUNA_DUPLICADA",
                $"{tabela.Nome}.{coluna}",
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"Duplicated column in table {tabela.Nome}: {coluna}.",
                    $"Coluna duplicada na tabela {tabela.Nome}: {coluna}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Inspect metadata consistency and rebuild affected objects if needed.",
                    "Inspecione consistência de metadados e recrie objetos afetados se necessário."));
        }
    }

    private static void ValidarTiposDesconhecidos(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        foreach (var coluna in tabela.Colunas)
        {
            if (!coluna.TipoSql.StartsWith("TYPE_", StringComparison.OrdinalIgnoreCase))
                continue;

            contexto.AdicionarAchado(
                "high",
                "TIPO_DESCONHECIDO",
                $"{tabela.Nome}.{coluna.Nome}",
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"Column {tabela.Nome}.{coluna.Nome} has unknown type mapping: {coluna.TipoSql}.",
                    $"Coluna {tabela.Nome}.{coluna.Nome} possui mapeamento de tipo desconhecido: {coluna.TipoSql}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Validate database version compatibility and inspect field definition in RDB$FIELDS.",
                    "Valide compatibilidade de versão e confira a definição no RDB$FIELDS."));
        }
    }

    private static void ValidarPk(TabelaSchema tabela, ContextoAnaliseDdl contexto)
    {
        if (tabela.ChavePrimaria is null)
        {
            contexto.AdicionarAchado(
                "high",
                "TABELA_SEM_PK",
                tabela.Nome,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"Table {tabela.Nome} has no primary key.",
                    $"Tabela {tabela.Nome} não possui chave primária."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Review if this is expected. Missing PK may hide duplicate rows over time.",
                    "Revise se isso é esperado. Ausência de PK pode mascarar duplicidades."));
            return;
        }

        if (tabela.ChavePrimaria.Colunas.Count == 0)
        {
            contexto.AdicionarAchado(
                "critical",
                "PK_SEM_COLUNAS",
                tabela.Nome,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"Primary key {tabela.ChavePrimaria.Nome} in {tabela.Nome} has no columns.",
                    $"Chave primária {tabela.ChavePrimaria.Nome} em {tabela.Nome} não possui colunas."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Rebuild this PK from validated column metadata.",
                    "Recrie esta PK a partir de metadados validados."));
            return;
        }

        var colunas = tabela.Colunas.ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);
        foreach (var colunaPk in tabela.ChavePrimaria.Colunas)
        {
            if (colunas.ContainsKey(colunaPk))
                continue;

            contexto.AdicionarAchado(
                "critical",
                "PK_REFERENCIA_COLUNA_INEXISTENTE",
                tabela.Nome,
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    $"PK {tabela.ChavePrimaria.Nome} references missing column {colunaPk}.",
                    $"PK {tabela.ChavePrimaria.Nome} referencia coluna inexistente {colunaPk}."),
                TextoLocalizado.Obter(
                    contexto.Idioma,
                    "Recreate PK and validate relation fields catalog.",
                    "Recrie a PK e valide o catálogo de campos da relação."));
        }
    }
}
