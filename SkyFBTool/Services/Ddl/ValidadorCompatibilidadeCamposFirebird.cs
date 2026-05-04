using System.Text.RegularExpressions;

namespace SkyFBTool.Services.Ddl;

public static class ValidadorCompatibilidadeCamposFirebird
{
    private const int LimiteBytesTexto = 32_765;

    private static readonly Regex RegexTamanhoTexto = new(
        @"^(?<tipo>CHAR|VARCHAR)\s*\(\s*(?<tamanho>\d+)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexNumeroFixo = new(
        @"^(?<tipo>NUMERIC|DECIMAL)\s*\(\s*(?<precisao>\d+)\s*(?:,\s*(?<escala>\d+)\s*)?\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ResultadoAuditoriaCompatibilidadeCamposDdl Auditar(
        SnapshotSchema snapshot,
        int? versaoMajor = null,
        bool incluirItensOk = false)
    {
        var resultado = new ResultadoAuditoriaCompatibilidadeCamposDdl
        {
            VersaoServidor = snapshot.VersaoServidor,
            VersaoMajor = versaoMajor ?? snapshot.VersaoMajor
        };

        var itens = new List<ItemAuditoriaCompatibilidadeCampoDdl>();

        foreach (var dominio in snapshot.Dominios.OrderBy(d => d.Nome, StringComparer.OrdinalIgnoreCase))
            itens.Add(AvaliarObjeto("domain", $"DOMINIO.{dominio.Nome}", dominio.TipoSql, dominio.CharsetNome, dominio.BytesPorCaracter, resultado.VersaoMajor));

        foreach (var tabela in snapshot.Tabelas.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var coluna in tabela.Colunas)
                itens.Add(AvaliarObjeto("column", $"{tabela.Nome}.{coluna.Nome}", coluna.TipoSql, coluna.CharsetNome, coluna.BytesPorCaracter, resultado.VersaoMajor));
        }

        resultado.TotalItens = itens.Count;
        resultado.TotalProblemas = itens.Count(i => i.Status != "ok");
        resultado.TotalOk = resultado.TotalItens - resultado.TotalProblemas;
        resultado.Itens = incluirItensOk
            ? itens
            : itens.Where(i => i.Status != "ok").ToList();
        return resultado;
    }

    public static IReadOnlyList<AchadoCompatibilidadeCampoDdl> Validar(SnapshotSchema snapshot, int? versaoMajor = null)
    {
        return Auditar(snapshot, versaoMajor)
            .Itens
            .Where(i => i.Status != "ok")
            .Select(i => new AchadoCompatibilidadeCampoDdl
            {
                Severidade = i.Status,
                Codigo = i.Codigo,
                Escopo = i.Escopo,
                TipoSql = i.TipoSql,
                CharsetNome = i.CharsetNome,
                BytesPorCaracter = i.BytesPorCaracter,
                TamanhoDeclarado = i.TamanhoDeclarado,
                TamanhoEfetivoBytes = i.TamanhoEfetivoBytes,
                VersaoMinimaMajor = i.VersaoMinimaMajor,
                Limite = i.Limite,
                Valor = i.Valor
            })
            .ToList();
    }

    public static IReadOnlyList<AchadoCompatibilidadeCampoDdl> Validar(TabelaSchema tabela, int? versaoMajor = null)
    {
        var snapshot = new SnapshotSchema
        {
            VersaoMajor = versaoMajor,
            Tabelas = [tabela]
        };

        return Validar(snapshot, versaoMajor);
    }

    private static ItemAuditoriaCompatibilidadeCampoDdl AvaliarObjeto(
        string tipoObjeto,
        string escopo,
        string tipoSql,
        string? charsetNome,
        int? bytesPorCaracter,
        int? versaoMajor)
    {
        if (string.IsNullOrWhiteSpace(tipoSql))
        {
            return new ItemAuditoriaCompatibilidadeCampoDdl
            {
                TipoObjeto = tipoObjeto,
                Escopo = escopo,
                TipoSql = string.Empty,
                Status = "critical",
                Codigo = "CAMPO_TIPO_VAZIO",
                Mensagem = "Tipo SQL vazio ou ausente.",
                Recomendacao = "Revalide a origem do metadado e o mapeamento do campo."
            };
        }

        string tipoNormalizado = tipoSql.Trim().ToUpperInvariant();

        if (TentarValidarTamanhoTexto(tipoObjeto, escopo, tipoNormalizado, charsetNome, bytesPorCaracter, out var itemTexto))
            return itemTexto!;

        if (TentarValidarNumeroFixo(tipoObjeto, escopo, tipoNormalizado, versaoMajor, out var itemNumero))
            return itemNumero!;

        if (TentarValidarTipoIncompativelPorVersao(tipoObjeto, escopo, tipoNormalizado, versaoMajor, out var itemTipo))
            return itemTipo!;

        return new ItemAuditoriaCompatibilidadeCampoDdl
        {
            TipoObjeto = tipoObjeto,
            Escopo = escopo,
            TipoSql = tipoNormalizado,
            Status = "ok",
            Codigo = "OK",
            Mensagem = $"Compatible with Firebird {(versaoMajor?.ToString() ?? "unknown")}.",
            Recomendacao = string.Empty
        };
    }

    private static bool TentarValidarTamanhoTexto(
        string tipoObjeto,
        string escopo,
        string tipoSql,
        string? charsetNome,
        int? bytesPorCaracter,
        out ItemAuditoriaCompatibilidadeCampoDdl? item)
    {
        item = null;
        var match = RegexTamanhoTexto.Match(tipoSql);
        if (!match.Success)
            return false;

        string tipo = match.Groups["tipo"].Value.ToUpperInvariant();
        int tamanhoDeclarado = int.Parse(match.Groups["tamanho"].Value);
        int bpc = Math.Max(1, bytesPorCaracter ?? 1);
        int tamanhoEfetivoBytes = tamanhoDeclarado * bpc;
        bool excedeLimite = tamanhoEfetivoBytes > LimiteBytesTexto;

        item = new ItemAuditoriaCompatibilidadeCampoDdl
        {
            TipoObjeto = tipoObjeto,
            Escopo = escopo,
            TipoSql = tipoSql,
            CharsetNome = string.IsNullOrWhiteSpace(charsetNome) ? null : charsetNome,
            BytesPorCaracter = bpc,
            TamanhoDeclarado = tamanhoDeclarado,
            TamanhoEfetivoBytes = tamanhoEfetivoBytes,
            Status = excedeLimite ? "critical" : "ok",
            Codigo = excedeLimite
                ? "CAMPO_TAMANHO_EFETIVO_EXCEDIDO"
                : "OK",
            Mensagem = excedeLimite
                ? $"Declared {tipo}({tamanhoDeclarado}) with charset {charsetNome ?? "default"} may require {tamanhoEfetivoBytes:N0} bytes, above Firebird limit of {LimiteBytesTexto:N0} bytes."
                : $"Declared {tipo}({tamanhoDeclarado}) with charset {charsetNome ?? "default"} fits within Firebird limit {LimiteBytesTexto:N0} bytes.",
            Recomendacao = excedeLimite
                ? "Reduce the declared size or switch to a narrower charset before recreating the object."
                : string.Empty,
            Limite = LimiteBytesTexto,
            Valor = tamanhoEfetivoBytes
        };
        return true;
    }

    private static bool TentarValidarNumeroFixo(
        string tipoObjeto,
        string escopo,
        string tipoSql,
        int? versaoMajor,
        out ItemAuditoriaCompatibilidadeCampoDdl? item)
    {
        item = null;
        var match = RegexNumeroFixo.Match(tipoSql);
        if (!match.Success)
            return false;

        int precisao = int.Parse(match.Groups["precisao"].Value);
        int escala = match.Groups["escala"].Success
            ? int.Parse(match.Groups["escala"].Value)
            : 0;

        if (precisao < 1 || precisao > 38 || escala < 0 || escala > precisao)
        {
            item = new ItemAuditoriaCompatibilidadeCampoDdl
            {
                TipoObjeto = tipoObjeto,
                Escopo = escopo,
                TipoSql = tipoSql,
                Status = "critical",
                Codigo = "CAMPO_PRECISAO_NUMERICA_INVALIDA",
                Mensagem = $"Invalid numeric precision/scale: {tipoSql}.",
                Recomendacao = "Correct the precision/scale definition before generating the script.",
                Limite = 38,
                Valor = precisao
            };
            return true;
        }

        if (versaoMajor.HasValue && versaoMajor.Value < 4 && precisao > 18)
        {
            item = new ItemAuditoriaCompatibilidadeCampoDdl
            {
                TipoObjeto = tipoObjeto,
                Escopo = escopo,
                TipoSql = tipoSql,
                Status = "critical",
                Codigo = "CAMPO_PRECISAO_NUMERICA_INCOMPATIVEL",
                Mensagem = $"Numeric precision {precisao} requires Firebird 4+.",
                Recomendacao = "Adjust the numeric precision to a Firebird version supported by the target environment.",
                VersaoMinimaMajor = 4,
                Limite = 18,
                Valor = precisao
            };
            return true;
        }

        item = new ItemAuditoriaCompatibilidadeCampoDdl
        {
            TipoObjeto = tipoObjeto,
            Escopo = escopo,
            TipoSql = tipoSql,
            Status = "ok",
            Codigo = "OK",
            Mensagem = $"Declared numeric precision {precisao} is compatible.",
            Recomendacao = string.Empty,
            Limite = 38,
            Valor = precisao
        };
        return true;
    }

    private static bool TentarValidarTipoIncompativelPorVersao(
        string tipoObjeto,
        string escopo,
        string tipoSql,
        int? versaoMajor,
        out ItemAuditoriaCompatibilidadeCampoDdl? item)
    {
        item = null;

        int? versaoMinima = tipoSql switch
        {
            "BOOLEAN" => 3,
            "DECFLOAT(16)" or "DECFLOAT(34)" => 4,
            "INT128" => 4,
            "TIME WITH TIME ZONE" => 4,
            "TIMESTAMP WITH TIME ZONE" => 4,
            _ => null
        };

        if (versaoMinima is null)
            return false;

        if (!versaoMajor.HasValue || versaoMajor.Value >= versaoMinima.Value)
        {
            item = new ItemAuditoriaCompatibilidadeCampoDdl
            {
                TipoObjeto = tipoObjeto,
                Escopo = escopo,
                TipoSql = tipoSql,
                Status = "ok",
                Codigo = "OK",
                Mensagem = $"{tipoSql} is compatible with Firebird {(versaoMajor?.ToString() ?? "unknown")}.",
                Recomendacao = string.Empty,
                VersaoMinimaMajor = versaoMinima
            };
            return true;
        }

        item = new ItemAuditoriaCompatibilidadeCampoDdl
        {
            TipoObjeto = tipoObjeto,
            Escopo = escopo,
            TipoSql = tipoSql,
            Status = "critical",
            Codigo = "CAMPO_TIPO_INCOMPATIVEL_VERSAO",
            Mensagem = $"{tipoSql} requires Firebird {versaoMinima}+.",
            Recomendacao = "Adjust the data type or target Firebird version before exporting the script.",
            VersaoMinimaMajor = versaoMinima
        };
        return true;
    }
}
