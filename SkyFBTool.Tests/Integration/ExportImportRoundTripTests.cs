using FirebirdSql.Data.FirebirdClient;
using SkyFBTool.Cli.Commands;
using SkyFBTool.Core;
using SkyFBTool.Infra;
using SkyFBTool.Services.Export;
using SkyFBTool.Services.Import;
using System.Text;
using Xunit;

namespace SkyFBTool.Tests.Integration;

public class ExportImportRoundTripTests
{
    [Fact]
    public async Task Export_CommitEvery_ComMaisDeMilLinhas_GeraCommitsPeriodicos()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_commit_every.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "export_commit_every.sql");
        const string tabela = "TESTE_EXPORT_COMMIT";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);
            await InserirMuitasLinhasAsync(arquivoBanco, "UTF8", tabela, 1200);

            var opcoes = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoSql, tabela);
            opcoes.CommitACada = 500;

            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoes, destino);

            string sql = await File.ReadAllTextAsync(arquivoSql, CharsetSql.ResolverEncodingLeituraSql("UTF8"));
            int totalCommits = ContarOcorrencias(sql, "COMMIT;");
            Assert.Equal(3, totalCommits);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Export_EscapeNewlines_AtivoEscapaQuebras_NoSqlGerado()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_escape.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "export_escape.sql");
        const string tabela = "TESTE_EXPORT_ESCAPE";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaEInserirLinhaAsync(arquivoBanco, "UTF8", tabela, "linha1\r\nlinha2");

            var opcoes = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoSql, tabela);
            opcoes.EscaparQuebrasDeLinha = true;

            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoes, destino);

            string sql = await File.ReadAllTextAsync(arquivoSql, CharsetSql.ResolverEncodingLeituraSql("UTF8"));
            Assert.Contains("\\r\\n", sql);
            Assert.DoesNotContain("linha1\r\nlinha2", sql);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Export_BlobFormat_HexEBase64_GeraRepresentacaoCorreta()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_blob.fdb");
        string arquivoHex = Path.Combine(pastaTemp, "export_blob_hex.sql");
        string arquivoBase64 = Path.Combine(pastaTemp, "export_blob_base64.sql");
        const string tabela = "TESTE_EXPORT_BLOB";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaBlobEInserirLinhaAsync(arquivoBanco, "UTF8", tabela, [0xDE, 0xAD, 0xBE, 0xEF], "texto blob");

            var opHex = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoHex, tabela);
            opHex.FormatoBlob = FormatoBlob.Hex;
            await using (var destinoHex = new DestinoArquivo(arquivoHex, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opHex, destinoHex);

            string sqlHex = await File.ReadAllTextAsync(arquivoHex, CharsetSql.ResolverEncodingLeituraSql("UTF8"));
            Assert.Contains("x'DEADBEEF'", sqlHex, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("texto blob", sqlHex, StringComparison.OrdinalIgnoreCase);

            var opBase64 = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoBase64, tabela);
            opBase64.FormatoBlob = FormatoBlob.Base64;
            await using (var destinoBase64 = new DestinoArquivo(arquivoBase64, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opBase64, destinoBase64);

            string sqlBase64 = await File.ReadAllTextAsync(arquivoBase64, CharsetSql.ResolverEncodingLeituraSql("UTF8"));
            Assert.Contains("3q2+7w==", sqlBase64);
            Assert.Contains("texto blob", sqlBase64, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Export_InsertModeUpsert_GeraUpdateOrInsertComMatchingPk()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_upsert.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "export_upsert.sql");
        const string tabela = "TESTE_EXPORT_UPSERT";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaComPkEInserirLinhaAsync(arquivoBanco, "UTF8", tabela, 1, "nome_1");

            var opcoes = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoSql, tabela);
            opcoes.ModoInsert = ModoInsertExportacao.Upsert;

            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoes, destino);

            string sql = await File.ReadAllTextAsync(arquivoSql, CharsetSql.ResolverEncodingLeituraSql("UTF8"));
            Assert.Contains(
                $"UPDATE OR INSERT INTO {tabela} (ID, NOME) VALUES (1, 'nome_1') MATCHING (ID);",
                sql,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Export_ForceWin1252_EmCharsetNone_GeraSetNamesWin1252()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_none.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "export_none.sql");
        const string tabela = "TESTE_EXPORT_NONE";

        try
        {
            await CriarBancoAsync(arquivoBanco, "NONE");
            await CriarTabelaEInserirLinhaAsync(arquivoBanco, "NONE", tabela, "a\u00E7\u00E3o");

            var opcoes = CriarOpcoesExportacao(arquivoBanco, null, arquivoSql, tabela);
            opcoes.ForcarWin1252 = true;

            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("WIN1252")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoes, destino);

            string sql = await File.ReadAllTextAsync(arquivoSql, CharsetSql.ResolverEncodingLeituraSql("WIN1252"));
            Assert.Contains("SET NAMES WIN1252;", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("a\u00E7\u00E3o", sql);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task ExportEImport_RoundTripUtf8_ExecutaNoBanco()
    {
        if (!IntegracaoHabilitada())
            return;

        const string valorEsperado = "a\u00E7\u00E3o";
        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "roundtrip_utf8.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "roundtrip_utf8.sql");

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaEInserirLinhaAsync(arquivoBanco, "UTF8", "TESTE_EXPORT", valorEsperado);

            var opcoesExportacao = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoSql, "TESTE_EXPORT");
            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoesExportacao, destino);

            await LimparTabelaAsync(arquivoBanco, "UTF8", "TESTE_EXPORT");

            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: false);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            string valor = await ObterNomeAsync(arquivoBanco, "UTF8", "TESTE_EXPORT");
            Assert.Equal(valorEsperado, valor);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task ExportEImport_RoundTripWin1252_ExecutaNoBanco()
    {
        if (!IntegracaoHabilitada())
            return;

        const string valorEsperado = "a\u00E7\u00E3o";
        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "roundtrip_win1252.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "roundtrip_win1252.sql");

        try
        {
            await CriarBancoAsync(arquivoBanco, "WIN1252");
            await CriarTabelaEInserirLinhaAsync(arquivoBanco, "WIN1252", "TESTE_EXPORT", valorEsperado);

            var opcoesExportacao = CriarOpcoesExportacao(arquivoBanco, "WIN1252", arquivoSql, "TESTE_EXPORT");
            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("WIN1252")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoesExportacao, destino);

            await LimparTabelaAsync(arquivoBanco, "WIN1252", "TESTE_EXPORT");

            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: false);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            string valor = await ObterNomeAsync(arquivoBanco, "WIN1252", "TESTE_EXPORT");
            Assert.Equal(valorEsperado, valor);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task ExportEImport_IgnoraCampoCalculado_NoArquivoEImportacao()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "roundtrip_computed.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "roundtrip_computed.sql");
        const string tabela = "TESTE_EXPORT_COMP";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaComCampoCalculadoEInserirLinhaAsync(arquivoBanco, "UTF8", tabela);

            var opcoesExportacao = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoSql, tabela);
            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoesExportacao, destino);

            string sqlGerado = await File.ReadAllTextAsync(arquivoSql, CharsetSql.ResolverEncodingLeituraSql("UTF8"));
            Assert.DoesNotContain("VALOR_X2", sqlGerado, StringComparison.OrdinalIgnoreCase);

            await LimparTabelaAsync(arquivoBanco, "UTF8", tabela);

            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: false);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            int totalLinhas = await ContarLinhasAsync(arquivoBanco, "UTF8", tabela);
            Assert.Equal(1, totalLinhas);

            int valorCalculado = await ObterValorCalculadoAsync(arquivoBanco, "UTF8", tabela, 1);
            Assert.Equal(14, valorCalculado);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Export_QueryFile_PreservaColunasEOrdemDaConsulta()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_query_file_ordem.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "export_query_file_ordem.sql");
        const string tabela = "TESTE_EXPORT_QUERY_FILE";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaComCampoCalculadoEInserirLinhaAsync(arquivoBanco, "UTF8", tabela);

            var opcoesExportacao = CriarOpcoesExportacao(arquivoBanco, "UTF8", arquivoSql, tabela);
            opcoesExportacao.ConsultaSqlCompleta = $"SELECT ID, VALOR_X2, VALOR FROM {tabela} WHERE ID = 1";

            await using (var destino = new DestinoArquivo(arquivoSql, 100, CharsetSql.ResolverEncodingLeituraSql("UTF8")))
                await ExportadorTabelaFirebird.ExportarAsync(opcoesExportacao, destino);

            string sqlGerado = await File.ReadAllTextAsync(arquivoSql, CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            Assert.Contains(
                $"INSERT INTO {tabela} (ID, VALOR_X2, VALOR) VALUES (1, 14, 7);",
                sqlGerado,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Import_ContinueOnError_RegistraLogEContinua()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_continue.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "import_continue.sql");
        const string tabela = "TESTE_IMPORT_CONTINUE";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            string conteudo = $"""
                               SET SQL DIALECT 3;
                               SET NAMES UTF8;

                               INSERT INTO {tabela} (ID, NOME) VALUES (1, 'ok');
                               INSERT INTO {tabela} (ID, COLUNA_INVALIDA) VALUES (2, 'erro');
                               INSERT INTO {tabela} (ID, NOME) VALUES (3, 'ok2');
                               COMMIT;
                               """;

            await File.WriteAllTextAsync(arquivoSql, conteudo, CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            using var _cwd = new WorkingDirectoryScope(pastaTemp);
            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: true);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            int totalLinhas = await ContarLinhasAsync(arquivoBanco, "UTF8", tabela);
            Assert.Equal(2, totalLinhas);

            string[] arquivosLog = Directory.GetFiles(
                pastaTemp,
                "*_import_log_*.log",
                SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(arquivosLog);
            string textoLog = await File.ReadAllTextAsync(arquivosLog.OrderByDescending(a => a).First());
            Assert.Contains("Erro ao executar SQL", textoLog);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Import_GrandeVolume_ComFalhaNoMeio_ContinuaERegistraErro()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_volume.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "import_volume.sql");
        const string tabela = "TESTE_IMPORT_VOLUME";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            var conteudo = new StringBuilder();
            conteudo.AppendLine("SET SQL DIALECT 3;");
            conteudo.AppendLine("SET NAMES UTF8;");

            for (int i = 1; i <= 400; i++)
            {
                conteudo.AppendLine($"INSERT INTO {tabela} (ID, NOME) VALUES ({i}, 'antes_{i}');");
            }

            conteudo.AppendLine($"INSERT INTO {tabela} (ID, COLUNA_INVALIDA) VALUES (9999, 'erro');");

            for (int i = 401; i <= 800; i++)
            {
                conteudo.AppendLine($"INSERT INTO {tabela} (ID, NOME) VALUES ({i}, 'depois_{i}');");
            }

            conteudo.AppendLine("COMMIT;");

            await File.WriteAllTextAsync(arquivoSql, conteudo.ToString(), CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            using var _cwd = new WorkingDirectoryScope(pastaTemp);
            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: true);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            int totalLinhas = await ContarLinhasAsync(arquivoBanco, "UTF8", tabela);
            Assert.Equal(800, totalLinhas);

            string[] arquivosLog = Directory.GetFiles(
                pastaTemp,
                "*_import_log_*.log",
                SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(arquivosLog);

            string textoLog = await File.ReadAllTextAsync(arquivosLog.OrderByDescending(a => a).First());
            Assert.Contains("Erro ao executar SQL", textoLog);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Import_GrandeVolume_ComFalhaIntermitenteNoMeio_ContinuaERegistraErro()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_volume_intermitente.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "import_volume_intermitente.sql");
        const string tabela = "TESTE_IMPORT_VOLUME";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            var sb = new StringBuilder();
            sb.AppendLine("SET SQL DIALECT 3;");
            sb.AppendLine("SET NAMES UTF8;");
            for (int i = 1; i <= 300; i++)
            {
                sb.AppendLine($"INSERT INTO {tabela} (ID, NOME) VALUES ({i}, 'antes_{i}');");
            }
            sb.AppendLine($"INSERT INTO {tabela} (ID, COLUNA_INVALIDA) VALUES (9999, 'erro');");
            for (int i = 301; i <= 600; i++)
            {
                sb.AppendLine($"INSERT INTO {tabela} (ID, NOME) VALUES ({i}, 'depois_{i}');");
            }
            sb.AppendLine("COMMIT;");

            await File.WriteAllTextAsync(arquivoSql, sb.ToString(), CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            using var _cwd = new WorkingDirectoryScope(pastaTemp);
            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: true);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            int totalLinhas = await ContarLinhasAsync(arquivoBanco, "UTF8", tabela);
            Assert.Equal(600, totalLinhas);

            string[] arquivosLog = Directory.GetFiles(
                pastaTemp,
                "*_import_log_*.log",
                SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(arquivosLog);
            string textoLog = await File.ReadAllTextAsync(arquivosLog.OrderByDescending(a => a).First());
            Assert.Contains("Erro ao executar SQL", textoLog);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Import_ComCommitIntermediario_InsereAposCommit()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_commit.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "import_commit.sql");
        const string tabela = "TESTE_IMPORT_COMMIT";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            string conteudo = $"""
                               SET SQL DIALECT 3;
                               SET NAMES UTF8;

                               INSERT INTO {tabela} (ID, NOME) VALUES (1, 'a');
                               COMMIT;
                               INSERT INTO {tabela} (ID, NOME) VALUES (2, 'b');
                               COMMIT;
                               """;

            await File.WriteAllTextAsync(arquivoSql, conteudo, CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: false);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            int totalLinhas = await ContarLinhasAsync(arquivoBanco, "UTF8", tabela);
            Assert.Equal(2, totalLinhas);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Import_ParserSetTermComentariosEStrings_ExecutaComandosCorretamente()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_parser.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "import_parser.sql");
        const string tabela = "TESTE_IMPORT_PARSER";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            string conteudo = $"""
                               SET SQL DIALECT 3;
                               SET NAMES UTF8;
                               -- comentario de linha
                               INSERT INTO {tabela} (ID, NOME) VALUES (1, 'texto com ; no meio');
                               /* comentario de bloco ; */
                               SET TERM ^ ;
                               EXECUTE BLOCK AS
                               BEGIN
                                 INSERT INTO {tabela} (ID, NOME) VALUES (2, 'via block');
                               END^
                               SET TERM ; ^
                               INSERT INTO {tabela} (ID, NOME) VALUES (3, 'final');
                               COMMIT;
                               """;

            await File.WriteAllTextAsync(arquivoSql, conteudo, CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: false);
            await ImportadorSql.ImportarAsync(opcoesImportacao);

            int totalLinhas = await ContarLinhasAsync(arquivoBanco, "UTF8", tabela);
            Assert.Equal(3, totalLinhas);

            string valorLinha1 = await ObterNomePorIdAsync(arquivoBanco, "UTF8", tabela, 1);
            Assert.Equal("texto com ; no meio", valorLinha1);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Import_Progresso_NaoContaCabecalhoSqlNoContadorDeLinhas()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_progresso.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "import_progresso.sql");
        const string tabela = "TESTE_IMPORT_PROGRESSO";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            string conteudo = $"""
                               SET SQL DIALECT 3;
                               SET NAMES UTF8;

                               INSERT INTO {tabela} (ID, NOME) VALUES (1, 'ok');
                               COMMIT;
                               """;

            await File.WriteAllTextAsync(arquivoSql, conteudo, CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: false);
            opcoesImportacao.ProgressoACada = 1;

            var saidaOriginal = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                await ImportadorSql.ImportarAsync(opcoesImportacao);
            }
            finally
            {
                Console.SetOut(saidaOriginal);
            }

            string saida = writer.ToString();
            Assert.Contains("Linhas: 1 | Comandos: 1", saida);
            Assert.DoesNotContain("Linhas: 4 | Comandos: 1", saida);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task ImportCommand_InputsBatch_ComMultiplosArquivos_ImportaTodos()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_batch_inline.fdb");
        string arquivoSql1 = Path.Combine(pastaTemp, "batch_001.sql");
        string arquivoSql2 = Path.Combine(pastaTemp, "batch_002.sql");
        const string tabela = "TESTE_IMPORT_BATCH_INLINE";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            string conteudo1 = $"""
                                SET SQL DIALECT 3;
                                SET NAMES UTF8;

                                INSERT INTO {tabela} (ID, NOME) VALUES (1, 'lote_1');
                                COMMIT;
                                """;

            string conteudo2 = $"""
                                SET SQL DIALECT 3;
                                SET NAMES UTF8;

                                INSERT INTO {tabela} (ID, NOME) VALUES (2, 'lote_2');
                                COMMIT;
                                """;

            await File.WriteAllTextAsync(arquivoSql1, conteudo1, CharsetSql.ResolverEncodingLeituraSql("UTF8"));
            await File.WriteAllTextAsync(arquivoSql2, conteudo2, CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            var saidaOriginal = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                await ImportCommand.ExecuteAsync(
                [
                    "--database", arquivoBanco,
                    "--inputs-batch", Path.Combine(pastaTemp, "batch_*.sql")
                ]);
            }
            finally
            {
                Console.SetOut(saidaOriginal);
            }

            int totalLinhas = await ContarLinhasAsync(arquivoBanco, "UTF8", tabela);
            Assert.Equal(2, totalLinhas);

            string saida = writer.ToString();
            Assert.True(
                saida.Contains("Importação em lote concluída.", StringComparison.OrdinalIgnoreCase) ||
                saida.Contains("Batch import finished.", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Import_SemContinueOnError_DeveLancarFalhaImportacaoComContexto()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "import_falha_contexto.fdb");
        string arquivoSql = Path.Combine(pastaTemp, "import_falha_contexto.sql");
        const string tabela = "TESTE_IMPORT_FALHA_CTX";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);

            string conteudo = $"""
                               SET SQL DIALECT 3;
                               SET NAMES UTF8;

                               INSERT INTO {tabela} (ID, NOME) VALUES (1, 'ok');
                               INSERT INTO {tabela} (ID, COLUNA_INVALIDA) VALUES (2, 'erro');
                               INSERT INTO {tabela} (ID, NOME) VALUES (3, 'nao_deve_executar');
                               COMMIT;
                               """;

            await File.WriteAllTextAsync(arquivoSql, conteudo, CharsetSql.ResolverEncodingLeituraSql("UTF8"));

            var opcoesImportacao = CriarOpcoesImportacao(arquivoBanco, arquivoSql, continuarEmCasoDeErro: false);

            var ex = await Assert.ThrowsAsync<FalhaImportacaoSqlException>(async () =>
                await ImportadorSql.ImportarAsync(opcoesImportacao));

            Assert.Equal(arquivoSql, ex.Arquivo);
            Assert.Equal(5, ex.LinhaInicioComando);
            Assert.Contains("COLUNA_INVALIDA", ex.ComandoSql, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Export_ContinueOnError_QuandoDestinoFalha_RegistraLog()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_continue.fdb");
        const string tabela = "TESTE_EXPORT_CONTINUE";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaEInserirLinhaAsync(arquivoBanco, "UTF8", tabela, "ok");

            var opcoesExportacao = CriarOpcoesExportacao(arquivoBanco, "UTF8", Path.Combine(pastaTemp, "ignorado.sql"), tabela);
            opcoesExportacao.ContinuarEmCasoDeErro = true;

            await using var destino = new DestinoComFalhaControlada();

            using var _cwd = new WorkingDirectoryScope(pastaTemp);
            await ExportadorTabelaFirebird.ExportarAsync(opcoesExportacao, destino);

            string caminhoLog = Path.Combine(pastaTemp, "erros_exportacao.log");
            Assert.True(File.Exists(caminhoLog));
            string textoLog = await File.ReadAllTextAsync(caminhoLog);
            Assert.Contains("Erro ao escrever linha", textoLog);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    [Fact]
    public async Task Export_ComFalhaIntermitenteNoDestino_RetentaEConcluiVolume()
    {
        if (!IntegracaoHabilitada())
            return;

        string pastaTemp = CriarPastaTemp();
        string arquivoBanco = Path.Combine(pastaTemp, "export_retry_intermitente.fdb");
        const string tabela = "TESTE_EXPORT_RETRY";

        try
        {
            await CriarBancoAsync(arquivoBanco, "UTF8");
            await CriarTabelaVaziaAsync(arquivoBanco, "UTF8", tabela);
            await InserirMuitasLinhasAsync(arquivoBanco, "UTF8", tabela, 300);

            var opcoesExportacao = CriarOpcoesExportacao(arquivoBanco, "UTF8", Path.Combine(pastaTemp, "ignorado.sql"), tabela);
            opcoesExportacao.CommitACada = 100;

            await using var destino = new DestinoComFalhaIntermitente();
            await ExportadorTabelaFirebird.ExportarAsync(opcoesExportacao, destino);

            Assert.Equal(300, destino.TotalInsertGravados);
            Assert.Equal(2, destino.TotalFalhasSimuladas);
            Assert.True(destino.TotalChamadas > destino.TotalInsertGravados);
        }
        finally
        {
            TentarExcluirDiretorio(pastaTemp);
        }
    }

    private static bool IntegracaoHabilitada()
    {
        string? flag = Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_RUN_INTEGRATION");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static OpcoesExportacao CriarOpcoesExportacao(string arquivoBanco, string? charset, string arquivoSql, string tabela)
    {
        return new OpcoesExportacao
        {
            Host = ObterHost(),
            Porta = ObterPorta(),
            Usuario = ObterUsuario(),
            Senha = ObterSenha(),
            Database = arquivoBanco,
            Tabela = tabela,
            Charset = charset,
            ArquivoSaida = arquivoSql,
            CommitACada = 1
        };
    }

    private static OpcoesImportacao CriarOpcoesImportacao(string arquivoBanco, string arquivoSql, bool continuarEmCasoDeErro)
    {
        return new OpcoesImportacao
        {
            Host = ObterHost(),
            Porta = ObterPorta(),
            Usuario = ObterUsuario(),
            Senha = ObterSenha(),
            Database = arquivoBanco,
            ArquivoEntrada = arquivoSql,
            ContinuarEmCasoDeErro = continuarEmCasoDeErro,
            ProgressoACada = 0
        };
    }

    private static async Task CriarBancoAsync(string arquivoBanco, string charset)
    {
        var csb = new FbConnectionStringBuilder
        {
            DataSource = ObterHost(),
            Port = ObterPorta(),
            UserID = ObterUsuario(),
            Password = ObterSenha(),
            Database = arquivoBanco,
            Charset = charset,
            Dialect = 3
        };

        FbConnection.CreateDatabase(csb.ConnectionString, pageSize: 8192, forcedWrites: false, overwrite: true);
        await Task.CompletedTask;
    }

    private static async Task CriarTabelaEInserirLinhaAsync(string arquivoBanco, string charset, string tabela, string nome)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using (var cmd = new FbCommand($"CREATE TABLE {tabela} (ID INTEGER, NOME VARCHAR(100));", conexao))
            await cmd.ExecuteNonQueryAsync();

        await using (var cmd = new FbCommand($"INSERT INTO {tabela} (ID, NOME) VALUES (1, @NOME);", conexao))
        {
            cmd.Parameters.AddWithValue("NOME", nome);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task CriarTabelaVaziaAsync(string arquivoBanco, string charset, string tabela)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using var cmd = new FbCommand($"CREATE TABLE {tabela} (ID INTEGER, NOME VARCHAR(100));", conexao);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CriarTabelaComPkEInserirLinhaAsync(
        string arquivoBanco,
        string charset,
        string tabela,
        int id,
        string nome)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        string sqlCreate = $"""
                            CREATE TABLE {tabela} (
                              ID INTEGER NOT NULL,
                              NOME VARCHAR(100),
                              CONSTRAINT PK_{tabela} PRIMARY KEY (ID)
                            );
                            """;

        await using (var cmd = new FbCommand(sqlCreate, conexao))
            await cmd.ExecuteNonQueryAsync();

        await using (var cmd = new FbCommand($"INSERT INTO {tabela} (ID, NOME) VALUES (@ID, @NOME);", conexao))
        {
            cmd.Parameters.AddWithValue("ID", id);
            cmd.Parameters.AddWithValue("NOME", nome);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task InserirMuitasLinhasAsync(string arquivoBanco, string charset, string tabela, int totalLinhas)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using var transacao = await conexao.BeginTransactionAsync();
        for (int i = 1; i <= totalLinhas; i++)
        {
            await using var cmd = new FbCommand($"INSERT INTO {tabela} (ID, NOME) VALUES (@ID, @NOME);", conexao, transacao);
            cmd.Parameters.AddWithValue("ID", i);
            cmd.Parameters.AddWithValue("NOME", $"nome_{i}");
            await cmd.ExecuteNonQueryAsync();
        }
        await transacao.CommitAsync();
    }

    private static async Task CriarTabelaBlobEInserirLinhaAsync(
        string arquivoBanco,
        string charset,
        string tabela,
        byte[] blobBinario,
        string blobTexto)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        string sqlCreate = $"""
                            CREATE TABLE {tabela} (
                              ID INTEGER,
                              DADOS_BIN BLOB SUB_TYPE 0,
                              DADOS_TXT BLOB SUB_TYPE TEXT
                            );
                            """;
        await using (var cmd = new FbCommand(sqlCreate, conexao))
            await cmd.ExecuteNonQueryAsync();

        await using (var cmd = new FbCommand($"INSERT INTO {tabela} (ID, DADOS_BIN, DADOS_TXT) VALUES (1, @BIN, @TXT);", conexao))
        {
            cmd.Parameters.AddWithValue("BIN", blobBinario);
            cmd.Parameters.AddWithValue("TXT", blobTexto);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task LimparTabelaAsync(string arquivoBanco, string charset, string tabela)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using var cmd = new FbCommand($"DELETE FROM {tabela};", conexao);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string> ObterNomeAsync(string arquivoBanco, string charset, string tabela)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using var cmd = new FbCommand($"SELECT NOME FROM {tabela} WHERE ID = 1;", conexao);
        object? valor = await cmd.ExecuteScalarAsync();
        return Convert.ToString(valor) ?? string.Empty;
    }

    private static async Task<string> ObterNomePorIdAsync(string arquivoBanco, string charset, string tabela, int id)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using var cmd = new FbCommand($"SELECT NOME FROM {tabela} WHERE ID = @ID;", conexao);
        cmd.Parameters.AddWithValue("ID", id);
        object? valor = await cmd.ExecuteScalarAsync();
        return Convert.ToString(valor) ?? string.Empty;
    }

    private static async Task CriarTabelaComCampoCalculadoEInserirLinhaAsync(string arquivoBanco, string charset, string tabela)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        string sqlCreate = $"""
                            CREATE TABLE {tabela} (
                              ID INTEGER,
                              VALOR INTEGER,
                              VALOR_X2 COMPUTED BY (VALOR * 2)
                            );
                            """;

        await using (var cmd = new FbCommand(sqlCreate, conexao))
            await cmd.ExecuteNonQueryAsync();

        string sqlInsert = $"INSERT INTO {tabela} (ID, VALOR) VALUES (1, 7);";
        await using (var cmd = new FbCommand(sqlInsert, conexao))
            await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> ContarLinhasAsync(string arquivoBanco, string charset, string tabela)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using var cmd = new FbCommand($"SELECT COUNT(*) FROM {tabela};", conexao);
        object? valor = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(valor);
    }

    private static async Task<int> ObterValorCalculadoAsync(string arquivoBanco, string charset, string tabela, int id)
    {
        await using var conexao = new FbConnection(CriarConnectionString(arquivoBanco, charset));
        await conexao.OpenAsync();

        await using var cmd = new FbCommand($"SELECT VALOR_X2 FROM {tabela} WHERE ID = @ID;", conexao);
        cmd.Parameters.AddWithValue("ID", id);
        object? valor = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(valor);
    }

    private static string CriarConnectionString(string arquivoBanco, string charset)
    {
        var csb = new FbConnectionStringBuilder
        {
            DataSource = ObterHost(),
            Port = ObterPorta(),
            UserID = ObterUsuario(),
            Password = ObterSenha(),
            Database = arquivoBanco,
            Charset = charset,
            Dialect = 3
        };

        return csb.ConnectionString;
    }

    private static string ObterHost()
    {
        return Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_HOST") ?? "localhost";
    }

    private static int ObterPorta()
    {
        string? porta = Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_PORT");
        return int.TryParse(porta, out int valor) ? valor : 3050;
    }

    private static string ObterUsuario()
    {
        return Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_USER") ?? "sysdba";
    }

    private static string ObterSenha()
    {
        return Environment.GetEnvironmentVariable("SKYFBTOOL_TEST_DB_PASSWORD") ?? "masterkey";
    }

    private static string CriarPastaTemp()
    {
        string pasta = Path.Combine(Path.GetTempPath(), "SkyFBTool.Tests.Integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    private static int ContarOcorrencias(string texto, string termo)
    {
        int total = 0;
        int inicio = 0;

        while (true)
        {
            int indice = texto.IndexOf(termo, inicio, StringComparison.OrdinalIgnoreCase);
            if (indice < 0)
                break;

            total++;
            inicio = indice + termo.Length;
        }

        return total;
    }

    private static void TentarExcluirDiretorio(string pasta)
    {
        if (!Directory.Exists(pasta))
            return;

        try
        {
            Directory.Delete(pasta, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class WorkingDirectoryScope : IDisposable
    {
        private readonly string _anterior;

        public WorkingDirectoryScope(string novoDiretorio)
        {
            _anterior = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(novoDiretorio);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_anterior);
        }
    }

    private sealed class DestinoComFalhaControlada : IDestinoArquivo
    {
        private bool _jaFalhou;

        public Task EscreverLinhaAsync(string linha)
        {
            if (!_jaFalhou && linha.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase))
            {
                _jaFalhou = true;
                throw new IOException("Falha simulada no destino");
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DestinoComFalhaIntermitente : IDestinoArquivo
    {
        private int _falhasRestantes = 2;

        public int TotalChamadas { get; private set; }

        public int TotalFalhasSimuladas { get; private set; }

        public int TotalInsertGravados { get; private set; }

        public Task EscreverLinhaAsync(string linha)
        {
            TotalChamadas++;

            if (_falhasRestantes > 0 && linha.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase))
            {
                _falhasRestantes--;
                TotalFalhasSimuladas++;
                throw new IOException("Falha intermitente simulada no destino");
            }

            if (linha.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase))
                TotalInsertGravados++;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
