using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Ddl;

public static class ExtratorDdlFirebird
{
    public static async Task<SnapshotSchema> ExtrairSnapshotAsync(OpcoesDdlExtracao opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Database))
            throw new ArgumentException("Banco nao informado (--database).");

        await using var conexao = FabricaConexaoFirebird.CriarConexao(
            opcoes.Host,
            opcoes.Porta,
            opcoes.Database,
            opcoes.Usuario,
            opcoes.Senha,
            opcoes.Charset);

        await conexao.OpenAsync();
        return await CarregarSnapshotAsync(conexao);
    }

    public static async Task<(string ArquivoSql, string ArquivoJson)> ExtrairAsync(OpcoesDdlExtracao opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Database))
            throw new ArgumentException("Banco nao informado (--database).");

        var (arquivoSql, arquivoJson) = ResolverArquivosSaida(opcoes);
        Directory.CreateDirectory(Path.GetDirectoryName(arquivoSql)!);

        var snapshot = await ExtrairSnapshotAsync(opcoes);

        var sql = GeradorDdlSql.Gerar(snapshot);
        await File.WriteAllTextAsync(arquivoSql, sql);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(arquivoJson, json);

        return (arquivoSql, arquivoJson);
    }

    private static async Task<SnapshotSchema> CarregarSnapshotAsync(FbConnection conexao)
    {
        var tabelas = await CarregarTabelasAsync(conexao);
        var snapshot = new SnapshotSchema();

        foreach (var nomeTabela in tabelas)
        {
            var tabela = new TabelaSchema
            {
                Nome = nomeTabela,
                Colunas = await CarregarColunasAsync(conexao, nomeTabela),
                ChavePrimaria = await CarregarChavePrimariaAsync(conexao, nomeTabela),
                ChavesEstrangeiras = await CarregarChavesEstrangeirasAsync(conexao, nomeTabela),
                Indices = await CarregarIndicesAsync(conexao, nomeTabela)
            };

            snapshot.Tabelas.Add(tabela);
        }

        snapshot.Tabelas = snapshot.Tabelas
            .OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return snapshot;
    }

    private static async Task<List<string>> CarregarTabelasAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT TRIM(r.rdb$relation_name)
                           FROM rdb$relations r
                           WHERE r.rdb$view_blr IS NULL
                             AND COALESCE(r.rdb$system_flag, 0) = 0
                           ORDER BY 1
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();
        var tabelas = new List<string>();

        while (await reader.ReadAsync())
            tabelas.Add(reader.GetString(0));

        return tabelas;
    }

    private static async Task<List<ColunaSchema>> CarregarColunasAsync(FbConnection conexao, string nomeTabela)
    {
        try
        {
            return await CarregarColunasComQueryAsync(conexao, nomeTabela, incluirCharacterLength: true);
        }
        catch (FbException ex) when (MensagemTokenDesconhecidoParaCharacterLength(ex))
        {
            return await CarregarColunasComQueryAsync(conexao, nomeTabela, incluirCharacterLength: false);
        }
    }
    
    private static async Task<List<ColunaSchema>> CarregarColunasComQueryAsync(
        FbConnection conexao,
        string nomeTabela,
        bool incluirCharacterLength)
    {
        string selectCharacterLength = incluirCharacterLength
            ? "f.rdb$character_length AS char_len,"
            : "CAST(NULL AS INTEGER) AS char_len,";

        string sql = $"""
                      SELECT
                          TRIM(rf.rdb$field_name) AS field_name,
                          f.rdb$field_type AS field_type,
                          f.rdb$field_sub_type AS field_sub_type,
                          f.rdb$field_length AS field_length,
                          f.rdb$field_precision AS field_precision,
                          f.rdb$field_scale AS field_scale,
                          {selectCharacterLength}
                          COALESCE(rf.rdb$null_flag, 0) AS null_flag,
                          rf.rdb$default_source AS default_source,
                          f.rdb$default_source AS domain_default_source,
                          f.rdb$computed_source AS computed_source
                      FROM rdb$relation_fields rf
                      JOIN rdb$fields f ON f.rdb$field_name = rf.rdb$field_source
                      WHERE rf.rdb$relation_name = @tabela
                      ORDER BY rf.rdb$field_position
                      """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@tabela", nomeTabela);
        await using var reader = await cmd.ExecuteReaderAsync();

        var colunas = new List<ColunaSchema>();
        while (await reader.ReadAsync())
        {
            int fieldType = LerInt(reader, "field_type");
            int fieldSubtype = LerInt(reader, "field_sub_type");
            int fieldLength = LerInt(reader, "field_length");
            int? precision = reader.IsDBNull(reader.GetOrdinal("field_precision"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("field_precision"));
            int scale = LerInt(reader, "field_scale");
            int? charLength = reader.IsDBNull(reader.GetOrdinal("char_len"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("char_len"));

            string? defaultSource = reader.IsDBNull(reader.GetOrdinal("default_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("default_source"));
            string? domainDefault = reader.IsDBNull(reader.GetOrdinal("domain_default_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("domain_default_source"));
            string? computedBy = reader.IsDBNull(reader.GetOrdinal("computed_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("computed_source"));

            string tipoSql = MapearTipoSql(fieldType, fieldSubtype, fieldLength, precision, scale, charLength);
            bool aceitaNulo = LerInt(reader, "null_flag") == 0;

            colunas.Add(new ColunaSchema
            {
                Nome = reader.GetString(reader.GetOrdinal("field_name")),
                TipoSql = tipoSql,
                AceitaNulo = aceitaNulo,
                DefaultSql = NormalizarDefault(defaultSource ?? domainDefault),
                ComputedBySql = NormalizarExpressao(computedBy)
            });
        }

        return colunas;
    }

    private static int LerInt(FbDataReader reader, string coluna, int valorPadrao = 0)
    {
        int ordinal = reader.GetOrdinal(coluna);
        if (reader.IsDBNull(ordinal))
            return valorPadrao;

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static bool MensagemTokenDesconhecidoParaCharacterLength(FbException ex)
    {
        string mensagem = ex.Message ?? string.Empty;
        return mensagem.Contains("Token unknown", StringComparison.OrdinalIgnoreCase)
               && mensagem.Contains("character_length", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ChavePrimariaSchema?> CarregarChavePrimariaAsync(FbConnection conexao, string nomeTabela)
    {
        const string sql = """
                           SELECT
                               TRIM(rc.rdb$constraint_name) AS constraint_name,
                               TRIM(seg.rdb$field_name) AS field_name
                           FROM rdb$relation_constraints rc
                           JOIN rdb$index_segments seg ON seg.rdb$index_name = rc.rdb$index_name
                           WHERE rc.rdb$relation_name = @tabela
                             AND rc.rdb$constraint_type = 'PRIMARY KEY'
                           ORDER BY seg.rdb$field_position
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@tabela", nomeTabela);
        await using var reader = await cmd.ExecuteReaderAsync();

        ChavePrimariaSchema? pk = null;
        while (await reader.ReadAsync())
        {
            pk ??= new ChavePrimariaSchema
            {
                Nome = reader.GetString(reader.GetOrdinal("constraint_name"))
            };
            pk.Colunas.Add(reader.GetString(reader.GetOrdinal("field_name")));
        }

        return pk;
    }

    private static async Task<List<ChaveEstrangeiraSchema>> CarregarChavesEstrangeirasAsync(FbConnection conexao, string nomeTabela)
    {
        const string sql = """
                           SELECT
                               TRIM(rc.rdb$constraint_name) AS constraint_name,
                               TRIM(seg.rdb$field_name) AS field_name,
                               TRIM(ref_rc.rdb$relation_name) AS ref_table,
                               TRIM(ref_seg.rdb$field_name) AS ref_field,
                               COALESCE(refc.rdb$update_rule, 'RESTRICT') AS update_rule,
                               COALESCE(refc.rdb$delete_rule, 'RESTRICT') AS delete_rule
                           FROM rdb$relation_constraints rc
                           JOIN rdb$ref_constraints refc ON refc.rdb$constraint_name = rc.rdb$constraint_name
                           JOIN rdb$relation_constraints ref_rc ON ref_rc.rdb$constraint_name = refc.rdb$const_name_uq
                           JOIN rdb$index_segments seg ON seg.rdb$index_name = rc.rdb$index_name
                           JOIN rdb$index_segments ref_seg ON ref_seg.rdb$index_name = ref_rc.rdb$index_name
                                                           AND ref_seg.rdb$field_position = seg.rdb$field_position
                           WHERE rc.rdb$relation_name = @tabela
                             AND rc.rdb$constraint_type = 'FOREIGN KEY'
                           ORDER BY rc.rdb$constraint_name, seg.rdb$field_position
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@tabela", nomeTabela);
        await using var reader = await cmd.ExecuteReaderAsync();

        var mapa = new Dictionary<string, ChaveEstrangeiraSchema>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            string nomeFk = reader.GetString(reader.GetOrdinal("constraint_name"));
            if (!mapa.TryGetValue(nomeFk, out var fk))
            {
                fk = new ChaveEstrangeiraSchema
                {
                    Nome = nomeFk,
                    TabelaReferencia = reader.GetString(reader.GetOrdinal("ref_table")),
                    RegraUpdate = reader.GetString(reader.GetOrdinal("update_rule")).Trim(),
                    RegraDelete = reader.GetString(reader.GetOrdinal("delete_rule")).Trim()
                };
                mapa.Add(nomeFk, fk);
            }

            fk.Colunas.Add(reader.GetString(reader.GetOrdinal("field_name")));
            fk.ColunasReferencia.Add(reader.GetString(reader.GetOrdinal("ref_field")));
        }

        return mapa.Values
            .OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<IndiceSchema>> CarregarIndicesAsync(FbConnection conexao, string nomeTabela)
    {
        const string sql = """
                           SELECT
                               TRIM(i.rdb$index_name) AS index_name,
                               COALESCE(i.rdb$unique_flag, 0) AS unique_flag,
                               COALESCE(i.rdb$index_type, 0) AS index_type,
                               TRIM(seg.rdb$field_name) AS field_name
                           FROM rdb$indices i
                           JOIN rdb$index_segments seg ON seg.rdb$index_name = i.rdb$index_name
                           LEFT JOIN rdb$relation_constraints rc ON rc.rdb$index_name = i.rdb$index_name
                           WHERE i.rdb$relation_name = @tabela
                             AND COALESCE(i.rdb$system_flag, 0) = 0
                             AND rc.rdb$constraint_name IS NULL
                           ORDER BY i.rdb$index_name, seg.rdb$field_position
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@tabela", nomeTabela);
        await using var reader = await cmd.ExecuteReaderAsync();

        var mapa = new Dictionary<string, IndiceSchema>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(reader.GetOrdinal("index_name"));
            if (!mapa.TryGetValue(nome, out var indice))
            {
                indice = new IndiceSchema
                {
                    Nome = nome,
                    Unico = reader.GetInt32(reader.GetOrdinal("unique_flag")) == 1,
                    Descendente = reader.GetInt32(reader.GetOrdinal("index_type")) == 1
                };
                mapa.Add(nome, indice);
            }

            indice.Colunas.Add(reader.GetString(reader.GetOrdinal("field_name")));
        }

        return mapa.Values
            .OrderBy(i => i.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string ArquivoSql, string ArquivoJson) ResolverArquivosSaida(OpcoesDdlExtracao opcoes)
    {
        string nomeBanco = Path.GetFileNameWithoutExtension(opcoes.Database);
        if (string.IsNullOrWhiteSpace(nomeBanco))
            nomeBanco = "schema";

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string padrao = $"{SanitizarNomeArquivo(nomeBanco)}_schema_{timestamp}";

        if (string.IsNullOrWhiteSpace(opcoes.Saida))
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), padrao);
            return ($"{basePath}.sql", $"{basePath}.schema.json");
        }

        string saida = opcoes.Saida.Trim();
        if (Directory.Exists(saida) || saida.EndsWith(Path.DirectorySeparatorChar) || saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string dir = Path.GetFullPath(saida);
            string basePath = Path.Combine(dir, padrao);
            return ($"{basePath}.sql", $"{basePath}.schema.json");
        }

        string semExtensao = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(saida)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(saida));

        return ($"{semExtensao}.sql", $"{semExtensao}.schema.json");
    }

    private static string SanitizarNomeArquivo(string valor)
    {
        foreach (char invalido in Path.GetInvalidFileNameChars())
            valor = valor.Replace(invalido, '_');
        return valor;
    }

    private static string? NormalizarDefault(string? defaultSql)
    {
        if (string.IsNullOrWhiteSpace(defaultSql))
            return null;

        string normalizado = NormalizarExpressao(defaultSql)!;
        if (normalizado.StartsWith("DEFAULT ", StringComparison.OrdinalIgnoreCase))
            return normalizado;

        return $"DEFAULT {normalizado}";
    }

    private static string? NormalizarExpressao(string? expressao)
    {
        if (string.IsNullOrWhiteSpace(expressao))
            return null;

        return string.Join(
            ' ',
            expressao
                .Split(new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string MapearTipoSql(
        int fieldType,
        int fieldSubtype,
        int fieldLength,
        int? precision,
        int scale,
        int? characterLength)
    {
        if (fieldSubtype is 1 or 2 && fieldType is 7 or 8 or 16 or 26)
        {
            string nome = fieldSubtype == 1 ? "NUMERIC" : "DECIMAL";
            int prec = precision ?? InferirPrecisao(fieldType, fieldLength);
            int escala = Math.Abs(scale);
            return $"{nome}({prec},{escala})";
        }

        return fieldType switch
        {
            7 => "SMALLINT",
            8 => "INTEGER",
            10 => "FLOAT",
            12 => "DATE",
            13 => "TIME",
            14 => $"CHAR({characterLength ?? fieldLength})",
            16 => "BIGINT",
            23 => "BOOLEAN",
            24 => "DECFLOAT(16)",
            25 => "DECFLOAT(34)",
            26 => "INT128",
            27 => "TIME WITH TIME ZONE",
            28 => "TIMESTAMP WITH TIME ZONE",
            35 => "TIMESTAMP",
            37 => $"VARCHAR({characterLength ?? fieldLength})",
            261 => fieldSubtype == 1 ? "BLOB SUB_TYPE TEXT" : $"BLOB SUB_TYPE {fieldSubtype}",
            _ => $"TYPE_{fieldType}"
        };
    }

    private static int InferirPrecisao(int fieldType, int fieldLength)
    {
        return fieldType switch
        {
            7 => 4,
            8 => 9,
            16 => 18,
            26 => 38,
            _ => Math.Max(1, fieldLength)
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
