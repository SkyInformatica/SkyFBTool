using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Core;
using SkyFBTool.Infra;

namespace SkyFBTool.Services.Ddl;

public static class ExtratorDdlFirebird
{
    public static async Task<SnapshotSchema> ExtrairSnapshotAsync(OpcoesDdlExtracao opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Database))
            throw new ArgumentException("Banco não informado (--database).");

        try
        {
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
        catch (Exception ex) when (ex is not FalhaExtracaoDdlException)
        {
            throw new FalhaExtracaoDdlException(opcoes.Database, ex);
        }
    }

    public static async Task<(string ArquivoSql, string ArquivoJson, string ArquivoAuditoria)> ExtrairAsync(OpcoesDdlExtracao opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Database))
            throw new ArgumentException("Banco não informado (--database).");

        var (arquivoSql, arquivoJson, arquivoAuditoria) = ResolverArquivosSaida(opcoes);
        Directory.CreateDirectory(Path.GetDirectoryName(arquivoSql)!);

        var snapshot = await ExtrairSnapshotAsync(opcoes);

        var sql = GeradorDdlSql.Gerar(snapshot);
        Encoding encodingSql = CharsetSql.ResolverEncodingLeituraSql(snapshot.CharsetBanco);
        await File.WriteAllTextAsync(arquivoSql, sql, encodingSql);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(arquivoJson, json);

        var auditoria = ValidadorCompatibilidadeCamposFirebird.Auditar(snapshot, snapshot.VersaoMajor);
        await File.WriteAllTextAsync(arquivoAuditoria, JsonSerializer.Serialize(auditoria, JsonOptions));

        return (arquivoSql, arquivoJson, arquivoAuditoria);
    }

    private static async Task<SnapshotSchema> CarregarSnapshotAsync(FbConnection conexao)
    {
        var tabelas = await CarregarTabelasAsync(conexao);
        var snapshot = new SnapshotSchema();
        snapshot.VersaoServidor = conexao.ServerVersion;
        snapshot.VersaoMajor = TryObterVersaoMajor(conexao.ServerVersion, out int major) ? major : null;
        snapshot.CharsetBanco = await CarregarCharsetBancoAsync(conexao);

        snapshot.Dominios = await CarregarDominiosAsync(conexao);
        snapshot.Sequencias = await CarregarSequenciasAsync(conexao);
        snapshot.Procedimentos = await CarregarProcedimentosAsync(conexao);
        snapshot.Funcoes = await CarregarFuncoesAsync(conexao);
        snapshot.FuncoesExternas = await CarregarFuncoesExternasAsync(conexao);
        snapshot.Views = await CarregarViewsAsync(conexao);
        snapshot.Gatilhos = await CarregarGatilhosAsync(conexao);

        foreach (var nomeTabela in tabelas)
        {
            var tabela = new TabelaSchema
            {
                Nome = nomeTabela,
                Colunas = await CarregarColunasAsync(conexao, nomeTabela),
                ChavePrimaria = await CarregarChavePrimariaAsync(conexao, nomeTabela),
                ChavesUnicas = await CarregarChavesUnicasAsync(conexao, nomeTabela),
                RestricoesCheck = await CarregarChecksAsync(conexao, nomeTabela),
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

    private static async Task<List<DominioSchema>> CarregarDominiosAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT
                               TRIM(f.rdb$field_name) AS domain_name,
                               f.rdb$field_type AS field_type,
                               f.rdb$field_sub_type AS field_sub_type,
                               f.rdb$field_length AS field_length,
                               f.rdb$field_precision AS field_precision,
                               f.rdb$field_scale AS field_scale,
                               f.rdb$character_length AS char_len,
                               TRIM(COALESCE(cs.rdb$character_set_name, '')) AS charset_name,
                               COALESCE(cs.rdb$bytes_per_character, 1) AS bytes_per_character,
                               COALESCE(f.rdb$null_flag, 0) AS null_flag,
                               f.rdb$default_source AS default_source,
                               f.rdb$validation_source AS validation_source
                           FROM rdb$fields f
                           LEFT JOIN rdb$character_sets cs ON cs.rdb$character_set_id = f.rdb$character_set_id
                           WHERE COALESCE(f.rdb$system_flag, 0) = 0
                           ORDER BY f.rdb$field_name
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();

        var dominios = new List<DominioSchema>();
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
            string? validationSource = reader.IsDBNull(reader.GetOrdinal("validation_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("validation_source"));
            string? charsetName = reader.IsDBNull(reader.GetOrdinal("charset_name"))
                ? null
                : reader.GetString(reader.GetOrdinal("charset_name"));
            int? bytesPorCaracter = reader.IsDBNull(reader.GetOrdinal("bytes_per_character"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("bytes_per_character"));

            string nomeDominio = reader.GetString(reader.GetOrdinal("domain_name"));
            if (EhDominioImplicito(nomeDominio))
                continue;

            dominios.Add(new DominioSchema
            {
                Nome = nomeDominio,
                TipoSql = MapearTipoSql(fieldType, fieldSubtype, fieldLength, precision, scale, charLength),
                CharsetNome = string.IsNullOrWhiteSpace(charsetName) ? null : charsetName,
                BytesPorCaracter = bytesPorCaracter,
                AceitaNulo = LerInt(reader, "null_flag") == 0,
                DefaultSql = NormalizarDefault(defaultSource),
                CheckSql = NormalizarExpressao(validationSource)
            });
        }

        return dominios;
    }

    private static async Task<string?> CarregarCharsetBancoAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT TRIM(RDB$CHARACTER_SET_NAME) AS charset_name
                           FROM RDB$DATABASE
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        if (reader.IsDBNull(reader.GetOrdinal("charset_name")))
            return null;

        string charset = reader.GetString(reader.GetOrdinal("charset_name")).Trim();
        return string.IsNullOrWhiteSpace(charset) ? null : charset;
    }

    private static async Task<List<SequenciaSchema>> CarregarSequenciasAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT TRIM(g.rdb$generator_name) AS generator_name
                           FROM rdb$generators g
                           WHERE COALESCE(g.rdb$system_flag, 0) = 0
                           ORDER BY g.rdb$generator_name
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();

        var sequencias = new List<SequenciaSchema>();
        while (await reader.ReadAsync())
        {
            sequencias.Add(new SequenciaSchema
            {
                Nome = reader.GetString(reader.GetOrdinal("generator_name"))
            });
        }

        return sequencias;
    }

    private static async Task<List<ProcedimentoSchema>> CarregarProcedimentosAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT
                               TRIM(p.rdb$procedure_name) AS procedure_name,
                               p.rdb$procedure_source AS procedure_source
                           FROM rdb$procedures p
                           WHERE COALESCE(p.rdb$system_flag, 0) = 0
                           ORDER BY p.rdb$procedure_name
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();

        var procedimentos = new List<ProcedimentoSchema>();
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(reader.GetOrdinal("procedure_name"));
            string? source = reader.IsDBNull(reader.GetOrdinal("procedure_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("procedure_source"));

            if (string.IsNullOrWhiteSpace(source))
                continue;

            var parametros = await CarregarParametrosProcedimentoAsync(conexao, nome);
            procedimentos.Add(new ProcedimentoSchema
            {
                Nome = nome,
                SourceSql = NormalizarEspacosFonte(source),
                ParametrosEntrada = parametros.Entrada,
                ParametrosSaida = parametros.Saida
            });
        }

        return procedimentos;
    }

    private static async Task<(List<ParametroProcedimentoSchema> Entrada, List<ParametroProcedimentoSchema> Saida)> CarregarParametrosProcedimentoAsync(
        FbConnection conexao,
        string nomeProcedimento)
    {
        const string sql = """
                           SELECT
                               TRIM(pp.rdb$parameter_name) AS parameter_name,
                               COALESCE(pp.rdb$parameter_type, 0) AS parameter_type,
                               pp.rdb$field_source AS field_source,
                               pp.rdb$default_source AS default_source,
                               COALESCE(pp.rdb$null_flag, 0) AS null_flag
                           FROM rdb$procedure_parameters pp
                           WHERE pp.rdb$procedure_name = @procedure_name
                             AND COALESCE(pp.rdb$system_flag, 0) = 0
                           ORDER BY pp.rdb$parameter_type, pp.rdb$parameter_number
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@procedure_name", nomeProcedimento);
        await using var reader = await cmd.ExecuteReaderAsync();

        var entrada = new List<ParametroProcedimentoSchema>();
        var saida = new List<ParametroProcedimentoSchema>();

        while (await reader.ReadAsync())
        {
            string nomeParametro = reader.GetString(reader.GetOrdinal("parameter_name"));
            int tipoParametro = LerInt(reader, "parameter_type");
        string? fieldSource = reader.IsDBNull(reader.GetOrdinal("field_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("field_source"));
            string? defaultSource = reader.IsDBNull(reader.GetOrdinal("default_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("default_source"));

            var parametro = new ParametroProcedimentoSchema
            {
                Nome = nomeParametro,
                TipoSql = await ObterTipoParametroAsync(conexao, fieldSource),
                AceitaNulo = LerInt(reader, "null_flag") == 0,
                DefaultSql = NormalizarDefault(defaultSource)
            };

            if (tipoParametro == 1)
                saida.Add(parametro);
            else
                entrada.Add(parametro);
        }

        return (entrada, saida);
    }

    private static async Task<string> ObterTipoParametroAsync(FbConnection conexao, string? fieldSource)
    {
        if (string.IsNullOrWhiteSpace(fieldSource))
            return "TYPE_UNKNOWN";

        string nomeCampo = fieldSource.Trim();

        const string sql = """
                           SELECT
                               f.rdb$field_type AS field_type,
                               f.rdb$field_sub_type AS field_sub_type,
                               f.rdb$field_length AS field_length,
                               f.rdb$field_precision AS field_precision,
                               f.rdb$field_scale AS field_scale,
                               f.rdb$character_length AS char_len
                           FROM rdb$fields f
                           WHERE f.rdb$field_name = @field_source
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@field_source", nomeCampo);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return "TYPE_UNKNOWN";

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

        return MapearTipoSql(fieldType, fieldSubtype, fieldLength, precision, scale, charLength);
    }

    private static async Task<List<FuncaoSchema>> CarregarFuncoesAsync(FbConnection conexao)
    {
        if (!TryObterVersaoMajor(conexao.ServerVersion, out int major) || major < 3)
            return [];

        const string sql = """
                           SELECT
                               TRIM(f.rdb$function_name) AS function_name,
                               f.rdb$function_source AS function_source
                           FROM rdb$functions f
                           WHERE COALESCE(f.rdb$system_flag, 0) = 0
                           ORDER BY f.rdb$function_name
                           """;

        try
        {
            await using var cmd = new FbCommand(sql, conexao);
            await using var reader = await cmd.ExecuteReaderAsync();

            var funcoes = new List<FuncaoSchema>();
            while (await reader.ReadAsync())
            {
                string nome = reader.GetString(reader.GetOrdinal("function_name"));
                string? source = reader.IsDBNull(reader.GetOrdinal("function_source"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("function_source"));

                if (string.IsNullOrWhiteSpace(source))
                    continue;

                funcoes.Add(new FuncaoSchema
                {
                    Nome = nome,
                    SourceSql = NormalizarEspacosFonte(source)
                });
            }

            return funcoes;
        }
        catch (FbException ex) when (MensagemTokenDesconhecidoParaFunctionSource(ex))
        {
            return [];
        }
    }

    private static async Task<List<FuncaoExternaSchema>> CarregarFuncoesExternasAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT
                               TRIM(f.rdb$function_name) AS function_name,
                               TRIM(COALESCE(f.rdb$entrypoint, '')) AS entrypoint,
                               TRIM(COALESCE(f.rdb$module_name, '')) AS module_name,
                               COALESCE(f.rdb$return_argument, 0) AS return_argument
                           FROM rdb$functions f
                           WHERE COALESCE(f.rdb$system_flag, 0) = 0
                             AND (COALESCE(f.rdb$module_name, '') <> '' OR COALESCE(f.rdb$entrypoint, '') <> '')
                           ORDER BY f.rdb$function_name
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();

        var funcoes = new Dictionary<string, FuncaoExternaBuilder>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(reader.GetOrdinal("function_name"));
            string entrypoint = reader.GetString(reader.GetOrdinal("entrypoint"));
            string moduleName = reader.GetString(reader.GetOrdinal("module_name"));
            int returnArgument = LerInt(reader, "return_argument");

            var funcao = new FuncaoExternaBuilder
            {
                Nome = nome,
                EntryPoint = entrypoint,
                ModuleName = moduleName,
                ReturnArgument = returnArgument > 0 ? returnArgument : null
            };

            foreach (var argumento in await CarregarArgumentosFuncaoExternaAsync(conexao, nome))
            {
                if (argumento.Posicao == 0)
                    funcao.Retorno = argumento;
                else
                    funcao.Argumentos.Add(argumento);
            }

            funcoes.Add(nome, funcao);
        }

        return funcoes.Values
            .Select(funcao => new FuncaoExternaSchema
            {
                Nome = funcao.Nome,
                SourceSql = GerarDeclaracaoFuncaoExterna(funcao)
            })
            .OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<FuncaoExternaArgumentoBuilder>> CarregarArgumentosFuncaoExternaAsync(
        FbConnection conexao,
        string nomeFuncao)
    {
        const string sql = """
                           SELECT
                               COALESCE(arg.rdb$argument_position, 0) AS argument_position,
                               COALESCE(arg.rdb$mechanism, 0) AS mechanism,
                               COALESCE(arg.rdb$field_type, 0) AS field_type,
                               COALESCE(arg.rdb$field_sub_type, 0) AS field_sub_type,
                               COALESCE(arg.rdb$field_length, 0) AS field_length,
                               arg.rdb$field_precision AS field_precision,
                               COALESCE(arg.rdb$field_scale, 0) AS field_scale,
                               arg.rdb$character_length AS char_len
                           FROM rdb$function_arguments arg
                           WHERE arg.rdb$function_name = @function_name
                           ORDER BY arg.rdb$argument_position
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@function_name", nomeFuncao);
        await using var reader = await cmd.ExecuteReaderAsync();

        var argumentos = new List<FuncaoExternaArgumentoBuilder>();
        while (await reader.ReadAsync())
        {
            int posicao = LerInt(reader, "argument_position");
            int mecanismo = LerInt(reader, "mechanism");
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

            argumentos.Add(new FuncaoExternaArgumentoBuilder
            {
                Posicao = posicao,
                TipoSql = FormatarTipoFuncaoExterna(fieldType, fieldSubtype, fieldLength, precision, scale, charLength),
                Mecanismo = mecanismo
            });
        }

        return argumentos;
    }

    private static async Task<List<GatilhoSchema>> CarregarGatilhosAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT
                               TRIM(t.rdb$trigger_name) AS trigger_name,
                               TRIM(t.rdb$relation_name) AS relation_name,
                               COALESCE(t.rdb$trigger_type, 0) AS trigger_type,
                               COALESCE(t.rdb$trigger_inactive, 0) AS trigger_inactive,
                               COALESCE(t.rdb$trigger_sequence, 0) AS trigger_sequence,
                               t.rdb$trigger_source AS trigger_source
                           FROM rdb$triggers t
                           WHERE COALESCE(t.rdb$system_flag, 0) = 0
                           ORDER BY t.rdb$trigger_name
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();

        var gatilhos = new List<GatilhoSchema>();
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(reader.GetOrdinal("trigger_name"));
            string? relacao = reader.IsDBNull(reader.GetOrdinal("relation_name"))
                ? null
                : reader.GetString(reader.GetOrdinal("relation_name"));
            int tipoTrigger = LerInt(reader, "trigger_type");
            int triggerInativo = LerInt(reader, "trigger_inactive");
            int sequencia = LerInt(reader, "trigger_sequence");
            string? source = reader.IsDBNull(reader.GetOrdinal("trigger_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("trigger_source"));

            if (string.IsNullOrWhiteSpace(source))
                continue;

            gatilhos.Add(new GatilhoSchema
            {
                Nome = nome,
                RelacaoNome = string.IsNullOrWhiteSpace(relacao) ? null : relacao,
                TipoTrigger = tipoTrigger,
                Ativo = triggerInativo == 0,
                Sequencia = sequencia,
                SourceSql = NormalizarEspacosFonte(source)
            });
        }

        return gatilhos;
    }

    private static async Task<List<ViewSchema>> CarregarViewsAsync(FbConnection conexao)
    {
        const string sql = """
                           SELECT
                               TRIM(r.rdb$relation_name) AS view_name,
                               r.rdb$view_source AS view_source
                           FROM rdb$relations r
                           WHERE r.rdb$view_blr IS NOT NULL
                             AND COALESCE(r.rdb$system_flag, 0) = 0
                           ORDER BY r.rdb$relation_name
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        await using var reader = await cmd.ExecuteReaderAsync();

        var views = new List<ViewSchema>();
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(reader.GetOrdinal("view_name"));
            string? source = reader.IsDBNull(reader.GetOrdinal("view_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("view_source"));

            if (string.IsNullOrWhiteSpace(source))
                continue;

            views.Add(new ViewSchema
            {
                Nome = nome,
                SelectSql = NormalizarExpressao(source) ?? string.Empty
            });
        }

        return views;
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
                              TRIM(COALESCE(cs.rdb$character_set_name, '')) AS charset_name,
                              COALESCE(cs.rdb$bytes_per_character, 1) AS bytes_per_character,
                              COALESCE(rf.rdb$null_flag, 0) AS null_flag,
                              rf.rdb$default_source AS default_source,
                              f.rdb$default_source AS domain_default_source,
                              f.rdb$computed_source AS computed_source
                      FROM rdb$relation_fields rf
                      JOIN rdb$fields f ON f.rdb$field_name = rf.rdb$field_source
                      LEFT JOIN rdb$character_sets cs ON cs.rdb$character_set_id = f.rdb$character_set_id
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
            string? charsetName = reader.IsDBNull(reader.GetOrdinal("charset_name"))
                ? null
                : reader.GetString(reader.GetOrdinal("charset_name"));
            int? bytesPorCaracter = reader.IsDBNull(reader.GetOrdinal("bytes_per_character"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("bytes_per_character"));

            string tipoSql = MapearTipoSql(fieldType, fieldSubtype, fieldLength, precision, scale, charLength);
            bool aceitaNulo = LerInt(reader, "null_flag") == 0;

            colunas.Add(new ColunaSchema
            {
                Nome = reader.GetString(reader.GetOrdinal("field_name")),
                TipoSql = tipoSql,
                CharsetNome = string.IsNullOrWhiteSpace(charsetName) ? null : charsetName,
                BytesPorCaracter = bytesPorCaracter,
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

    private static bool MensagemTokenDesconhecidoParaFunctionSource(FbException ex)
    {
        string mensagem = ex.Message ?? string.Empty;
        return mensagem.Contains("Token unknown", StringComparison.OrdinalIgnoreCase)
               && mensagem.Contains("function_source", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryObterVersaoMajor(string? serverVersion, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(serverVersion))
            return false;

        var match = Regex.Match(serverVersion, @"\bV(?<major>\d+)\.", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["major"].Value, out major))
            return true;

        match = Regex.Match(serverVersion, @"(?<major>\d+)\.(?<minor>\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["major"].Value, out major))
            return true;

        return false;
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
                               TRIM(rc.rdb$index_name) AS support_index_name,
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
                    IndiceSuporteNome = reader.GetString(reader.GetOrdinal("support_index_name")),
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

    private static async Task<List<ChaveUnicaSchema>> CarregarChavesUnicasAsync(FbConnection conexao, string nomeTabela)
    {
        const string sql = """
                           SELECT
                               TRIM(rc.rdb$constraint_name) AS constraint_name,
                               TRIM(seg.rdb$field_name) AS field_name
                           FROM rdb$relation_constraints rc
                           JOIN rdb$index_segments seg ON seg.rdb$index_name = rc.rdb$index_name
                           WHERE rc.rdb$relation_name = @tabela
                             AND rc.rdb$constraint_type = 'UNIQUE'
                           ORDER BY rc.rdb$constraint_name, seg.rdb$field_position
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@tabela", nomeTabela);
        await using var reader = await cmd.ExecuteReaderAsync();

        var mapa = new Dictionary<string, ChaveUnicaSchema>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(reader.GetOrdinal("constraint_name"));
            if (!mapa.TryGetValue(nome, out var unica))
            {
                unica = new ChaveUnicaSchema
                {
                    Nome = nome
                };
                mapa.Add(nome, unica);
            }

            unica.Colunas.Add(reader.GetString(reader.GetOrdinal("field_name")));
        }

        return mapa.Values
            .OrderBy(u => u.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<RestricaoCheckSchema>> CarregarChecksAsync(FbConnection conexao, string nomeTabela)
    {
        const string sql = """
                           SELECT
                               TRIM(rc.rdb$constraint_name) AS constraint_name,
                               TRIM(trg.rdb$trigger_source) AS trigger_source
                           FROM rdb$relation_constraints rc
                           JOIN rdb$check_constraints cc ON cc.rdb$constraint_name = rc.rdb$constraint_name
                           JOIN rdb$triggers trg ON trg.rdb$trigger_name = cc.rdb$trigger_name
                           WHERE rc.rdb$relation_name = @tabela
                             AND rc.rdb$constraint_type = 'CHECK'
                           ORDER BY rc.rdb$constraint_name
                           """;

        await using var cmd = new FbCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@tabela", nomeTabela);
        await using var reader = await cmd.ExecuteReaderAsync();

        var checks = new List<RestricaoCheckSchema>();
        while (await reader.ReadAsync())
        {
            string nome = reader.GetString(reader.GetOrdinal("constraint_name"));
            string? triggerSource = reader.IsDBNull(reader.GetOrdinal("trigger_source"))
                ? null
                : reader.GetString(reader.GetOrdinal("trigger_source"));

            string? checkSql = NormalizarCheckConstraintSource(triggerSource);
            if (string.IsNullOrWhiteSpace(checkSql))
                continue;

            checks.Add(new RestricaoCheckSchema
            {
                Nome = nome,
                CheckSql = checkSql
            });
        }

        return checks;
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

    private static (string ArquivoSql, string ArquivoJson, string ArquivoAuditoria) ResolverArquivosSaida(OpcoesDdlExtracao opcoes)
    {
        string nomeBanco = Path.GetFileNameWithoutExtension(opcoes.Database);
        if (string.IsNullOrWhiteSpace(nomeBanco))
            nomeBanco = "schema";

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string padrao = $"{SanitizarNomeArquivo(nomeBanco)}_schema_{timestamp}";

        if (string.IsNullOrWhiteSpace(opcoes.Saida))
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), padrao);
            return ($"{basePath}.sql", $"{basePath}.schema.json", $"{basePath}.schema.audit.json");
        }

        string saida = opcoes.Saida.Trim();
        if (Directory.Exists(saida) || saida.EndsWith(Path.DirectorySeparatorChar) || saida.EndsWith(Path.AltDirectorySeparatorChar))
        {
            string dir = Path.GetFullPath(saida);
            string basePath = Path.Combine(dir, padrao);
            return ($"{basePath}.sql", $"{basePath}.schema.json", $"{basePath}.schema.audit.json");
        }

        string semExtensao = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(saida)) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(saida));

        return ($"{semExtensao}.sql", $"{semExtensao}.schema.json", $"{semExtensao}.schema.audit.json");
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

    private static string NormalizarEspacosFonte(string fonte)
    {
        return fonte.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }

    private static bool EhDominioImplicito(string nomeDominio)
    {
        return nomeDominio.StartsWith("RDB$", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizarCheckConstraintSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        string normalizado = NormalizarExpressao(source)!;
        if (normalizado.StartsWith("CHECK ", StringComparison.OrdinalIgnoreCase))
            return normalizado;

        int idx = normalizado.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return normalizado[idx..].Trim();

        if (normalizado.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase))
            return null;

        return $"CHECK ({normalizado})";
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

    private static string GerarDeclaracaoFuncaoExterna(FuncaoExternaBuilder funcao)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DECLARE EXTERNAL FUNCTION {GeradorDdlSql.Q(funcao.Nome)}");

        if (funcao.Argumentos.Count > 0)
        {
            sb.AppendLine("(");
            sb.AppendLine(string.Join("," + Environment.NewLine, funcao.Argumentos
                .OrderBy(a => a.Posicao)
                .Select(FormatarArgumentoFuncaoExterna)));
            sb.AppendLine(")");
        }

        if (funcao.Retorno is not null)
            sb.AppendLine($"RETURNS {FormatarRetornoFuncaoExterna(funcao.Retorno)}");
        else if (funcao.ReturnArgument is not null && funcao.ReturnArgument > 0)
            sb.AppendLine($"RETURNS PARAMETER {funcao.ReturnArgument}");

        sb.AppendLine($"ENTRY_POINT '{funcao.EntryPoint}'");
        sb.Append($"MODULE_NAME '{funcao.ModuleName}';");
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string FormatarArgumentoFuncaoExterna(FuncaoExternaArgumentoBuilder argumento)
    {
        var sb = new StringBuilder();
        sb.Append(argumento.TipoSql);

        if (argumento.Mecanismo == 2 || argumento.Mecanismo == 3)
            sb.Append(" BY DESCRIPTOR");
        else if (argumento.Mecanismo == 4)
            sb.Append(" BY SCALAR_ARRAY");
        else if (argumento.Mecanismo == 5)
            sb.Append(" NULL");

        return sb.ToString();
    }

    private static string FormatarRetornoFuncaoExterna(FuncaoExternaArgumentoBuilder retorno)
    {
        return FormatarArgumentoFuncaoExterna(retorno);
    }

    private static string FormatarTipoFuncaoExterna(
        int fieldType,
        int fieldSubtype,
        int fieldLength,
        int? precision,
        int scale,
        int? characterLength)
    {
        if (fieldType == 40)
            return $"CSTRING({characterLength ?? fieldLength})";

        return MapearTipoSql(fieldType, fieldSubtype, fieldLength, precision, scale, characterLength);
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

    private sealed class FuncaoExternaBuilder
    {
        public string Nome { get; init; } = string.Empty;
        public string EntryPoint { get; init; } = string.Empty;
        public string ModuleName { get; init; } = string.Empty;
        public int? ReturnArgument { get; init; }
        public FuncaoExternaArgumentoBuilder? Retorno { get; set; }
        public List<FuncaoExternaArgumentoBuilder> Argumentos { get; } = [];
    }

    private sealed class FuncaoExternaArgumentoBuilder
    {
        public int Posicao { get; init; }
        public string TipoSql { get; init; } = string.Empty;
        public int Mecanismo { get; init; }
    }
}
