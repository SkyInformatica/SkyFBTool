using SkyFBTool.Core;

namespace SkyFBTool.Services.Ddl.Rules;

internal sealed class RegraCompatibilidadeCamposDdl : IRegraAnaliseDdl
{
    public void Avaliar(ContextoAnaliseDdl contexto)
    {
        var achados = ValidadorCompatibilidadeCamposFirebird.Validar(contexto.Snapshot, contexto.Snapshot.VersaoMajor);
        foreach (var achado in achados)
        {
            contexto.AdicionarAchado(
                achado.Severidade,
                achado.Codigo,
                achado.Escopo,
                MontarDescricao(achado, contexto.Idioma),
                MontarRecomendacao(achado, contexto.Idioma));
        }
    }

    private static string MontarDescricao(AchadoCompatibilidadeCampoDdl achado, IdiomaSaida idioma)
    {
        return achado.Codigo switch
        {
            "CAMPO_TAMANHO_CHAR_EXCEDIDO" => TextoLocalizado.Obter(
                idioma,
                $"Column {achado.Escopo} declares CHAR({achado.Valor:N0}), above the Firebird limit of {achado.Limite:N0} bytes.",
                $"Coluna {achado.Escopo} declara CHAR({achado.Valor:N0}), acima do limite do Firebird de {achado.Limite:N0} bytes."),
            "CAMPO_TAMANHO_VARCHAR_EXCEDIDO" => TextoLocalizado.Obter(
                idioma,
                $"Column {achado.Escopo} declares VARCHAR({achado.Valor:N0}), above the Firebird limit of {achado.Limite:N0} bytes.",
                $"Coluna {achado.Escopo} declara VARCHAR({achado.Valor:N0}), acima do limite do Firebird de {achado.Limite:N0} bytes."),
            "CAMPO_PRECISAO_NUMERICA_INVALIDA" => TextoLocalizado.Obter(
                idioma,
                $"Column {achado.Escopo} declares an invalid numeric precision/scale for Firebird ({achado.TipoSql}).",
                $"Coluna {achado.Escopo} declara precisão/escala numérica inválida para Firebird ({achado.TipoSql})."),
            "CAMPO_PRECISAO_NUMERICA_INCOMPATIVEL" => TextoLocalizado.Obter(
                idioma,
                $"Column {achado.Escopo} uses {achado.TipoSql}, which requires Firebird {achado.VersaoMinimaMajor}+.",
                $"Coluna {achado.Escopo} usa {achado.TipoSql}, que exige Firebird {achado.VersaoMinimaMajor}+."),
            "CAMPO_TIPO_INCOMPATIVEL_VERSAO" => TextoLocalizado.Obter(
                idioma,
                $"Column {achado.Escopo} uses {achado.TipoSql}, which requires Firebird {achado.VersaoMinimaMajor}+.",
                $"Coluna {achado.Escopo} usa {achado.TipoSql}, que exige Firebird {achado.VersaoMinimaMajor}+."),
            _ => TextoLocalizado.Obter(
                idioma,
                $"Column {achado.Escopo} has a compatibility issue ({achado.Codigo}).",
                $"Coluna {achado.Escopo} possui um problema de compatibilidade ({achado.Codigo}).")
        };
    }

    private static string MontarRecomendacao(AchadoCompatibilidadeCampoDdl achado, IdiomaSaida idioma)
    {
        return achado.Codigo switch
        {
            "CAMPO_TAMANHO_CHAR_EXCEDIDO" => TextoLocalizado.Obter(
                idioma,
                "Reduce the declared size or revisit the charset/encoding strategy before recreating the column.",
                "Reduza o tamanho declarado ou revise a estratégia de charset/encoding antes de recriar a coluna."),
            "CAMPO_TAMANHO_VARCHAR_EXCEDIDO" => TextoLocalizado.Obter(
                idioma,
                "Reduce the declared size or revisit the charset/encoding strategy before recreating the column.",
                "Reduza o tamanho declarado ou revise a estratégia de charset/encoding antes de recriar a coluna."),
            "CAMPO_PRECISAO_NUMERICA_INVALIDA" => TextoLocalizado.Obter(
                idioma,
                "Correct the precision/scale definition before generating the script.",
                "Corrija a definição de precisão/escala antes de gerar o script."),
            "CAMPO_PRECISAO_NUMERICA_INCOMPATIVEL" => TextoLocalizado.Obter(
                idioma,
                "Adjust the numeric precision to a Firebird version supported by the target environment.",
                "Ajuste a precisão numérica para uma versão do Firebird suportada pelo ambiente alvo."),
            "CAMPO_TIPO_INCOMPATIVEL_VERSAO" => TextoLocalizado.Obter(
                idioma,
                "Adjust the data type or target Firebird version before exporting the script.",
                "Ajuste o tipo de dado ou a versão alvo do Firebird antes de exportar o script."),
            _ => TextoLocalizado.Obter(
                idioma,
                "Review the column definition and compare it with the target Firebird version.",
                "Revise a definição da coluna e compare com a versão alvo do Firebird.")
        };
    }
}
