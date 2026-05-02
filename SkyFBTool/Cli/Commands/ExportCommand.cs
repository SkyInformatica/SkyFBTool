using System.Text;
using SkyFBTool.Cli.Common;
using SkyFBTool.Core;
using SkyFBTool.Infra;
using SkyFBTool.Services.Ddl;
using SkyFBTool.Services.Export;

namespace SkyFBTool.Cli.Commands;

public static class ExportCommand
{
    private const long LimiteAvisoArquivoBytes = 64L * 1024;

    public static async Task ExecuteAsync(string[] args)
    {
        IdiomaSaida idioma = IdiomaSaidaDetector.Detectar();
        var op = new OpcoesExportacao();

        for (int i = 0; i < args.Length; i++)
        {
            string chave = args[i].TrimStart('-').ToLowerInvariant();

            switch (chave)
            {
                case "database":
                    op.Database = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "table":
                    op.Tabela = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "target-table":
                    op.AliasTabela = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "output":
                    op.ArquivoSaida = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "host":
                    op.Host = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "port":
                    op.Porta = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "user":
                    op.Usuario = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "password":
                    op.Senha = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "charset":
                    op.Charset = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    break;
                case "filter":
                    op.CondicaoWhere = ResolverCondicaoWhere(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "filter-file":
                    op.CondicaoWhere = LerCondicaoWhereDeArquivo(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "query-file":
                    op.ConsultaSqlCompleta = LerConsultaSqlDeArquivo(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "blob-format":
                    var valorBlobFormat = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    op.FormatoBlob = Enum.TryParse<FormatoBlob>(valorBlobFormat, true, out var fmt)
                        ? fmt
                        : FormatoBlob.Hex;
                    break;
                case "insert-mode":
                    var valorInsertMode = CliArgumentParser.LerValorOpcao(args, ref i, chave);
                    if (!Enum.TryParse<ModoInsertExportacao>(valorInsertMode, true, out var modoInsert))
                    {
                        throw new ArgumentException(CliText.Texto(
                            idioma,
                            $"Invalid value for --insert-mode: {valorInsertMode}. Use: insert | upsert",
                            $"Valor inválido para --insert-mode: {valorInsertMode}. Use: insert | upsert"));
                    }

                    op.ModoInsert = modoInsert;
                    break;
                case "commit-every":
                    op.CommitACada = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "progress-every":
                    op.ProgressoACada = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "split-size-mb":
                    op.TamanhoMaximoArquivoMb = int.Parse(CliArgumentParser.LerValorOpcao(args, ref i, chave));
                    break;
                case "legacy-win1252":
                    op.ForcarWin1252 = true;
                    break;
                case "sanitize-text":
                    op.SanitizarTexto = true;
                    break;
                case "escape-newlines":
                    op.EscaparQuebrasDeLinha = true;
                    break;
                case "continue-on-error":
                    op.ContinuarEmCasoDeErro = true;
                    break;
                default:
                    throw new ArgumentException(CliText.Texto(
                        idioma,
                        $"Unknown option: --{chave}",
                        $"Opção desconhecida: --{chave}"));
            }
        }

        ValidarCombinacaoOpcoesExportacao(op, idioma);
        ExibirResumoModoExportacao(op);

        var arquivoSaidaInformado = op.ArquivoSaida;
        op.ArquivoSaida = ResolverArquivoSaidaExportacao(op);

        if (string.IsNullOrWhiteSpace(arquivoSaidaInformado))
        {
            Console.WriteLine(CliText.Texto(
                idioma,
                $"Output file was not provided. Using: {op.ArquivoSaida}",
                $"Arquivo de saída não informado. Usando: {op.ArquivoSaida}"));
        }
        else if (EhDiretorio(arquivoSaidaInformado))
        {
            Console.WriteLine(CliText.Texto(
                idioma,
                $"Directory provided in --output. Generated file: {op.ArquivoSaida}",
                $"Diretório informado em --output. Arquivo gerado: {op.ArquivoSaida}"));
        }

        if (op.TamanhoMaximoArquivoMb > 0)
            Console.WriteLine(CliText.Texto(
                idioma,
                $"File split enabled: {op.TamanhoMaximoArquivoMb} MB per file.",
                $"Divisão de arquivo ativa: {op.TamanhoMaximoArquivoMb} MB por arquivo."));
        else
            Console.WriteLine(CliText.Texto(idioma, "File split disabled.", "Divisão de arquivo desativada."));

        Console.WriteLine(CliText.Texto(idioma, "Starting export...", "Iniciando exportação..."));

        var encodingSaida = ResolverEncodingSaidaExportacao(op);
        await using var destino = new DestinoArquivo(op.ArquivoSaida, op.TamanhoMaximoArquivoMb, encodingSaida);
        await ExportadorTabelaFirebird.ExportarAsync(op, destino);
        ExibirResumoArquivosExportacao(destino.ObterArquivosGerados());
    }

    private static string GerarNomeArquivoExportacao(OpcoesExportacao op)
    {
        var baseNome = !string.IsNullOrWhiteSpace(op.AliasTabela)
            ? op.AliasTabela
            : op.Tabela;

        if (string.IsNullOrWhiteSpace(baseNome))
            baseNome = "exportacao";

        foreach (char invalido in Path.GetInvalidFileNameChars())
            baseNome = baseNome.Replace(invalido, '_');

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        return $"{baseNome}_{timestamp}.sql";
    }

    private static string ResolverArquivoSaidaExportacao(OpcoesExportacao op)
    {
        var nomeGerado = GerarNomeArquivoExportacao(op);
        var output = op.ArquivoSaida?.Trim();

        if (string.IsNullOrWhiteSpace(output))
            return Path.Combine(Directory.GetCurrentDirectory(), nomeGerado);

        if (EhDiretorio(output))
        {
            var diretorio = Path.GetFullPath(output);
            Directory.CreateDirectory(diretorio);
            return Path.Combine(diretorio, nomeGerado);
        }

        return output;
    }

    private static bool EhDiretorio(string caminho)
    {
        if (Directory.Exists(caminho))
            return true;

        return caminho.EndsWith(Path.DirectorySeparatorChar)
               || caminho.EndsWith(Path.AltDirectorySeparatorChar);
    }

    private static string? ResolverCondicaoWhere(string valor)
    {
        string candidato = valor.Trim();

        if (File.Exists(candidato))
            return LerCondicaoWhereDeArquivo(candidato);

        return candidato;
    }

    private static string LerCondicaoWhereDeArquivo(string caminhoArquivo)
    {
        if (!File.Exists(caminhoArquivo))
            throw new FileNotFoundException($"Arquivo de condição WHERE não encontrado: {caminhoArquivo}");

        ExibirAvisoArquivoGrande(caminhoArquivo, "condição WHERE");

        string conteudo = File.ReadAllText(caminhoArquivo).Trim();
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ArgumentException($"Arquivo de condição WHERE vazio: {caminhoArquivo}");

        return conteudo;
    }

    private static string LerConsultaSqlDeArquivo(string caminhoArquivo)
    {
        if (!File.Exists(caminhoArquivo))
            throw new FileNotFoundException($"Arquivo de consulta SQL não encontrado: {caminhoArquivo}");

        ExibirAvisoArquivoGrande(caminhoArquivo, "consulta SQL");

        string conteudo = File.ReadAllText(caminhoArquivo).Trim();
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ArgumentException($"Arquivo de consulta SQL vazio: {caminhoArquivo}");

        return conteudo;
    }

    private static void ExibirAvisoArquivoGrande(string caminhoArquivo, string tipoConteudo)
    {
        var info = new FileInfo(caminhoArquivo);
        if (info.Length <= LimiteAvisoArquivoBytes)
            return;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(
            $"Aviso: o arquivo de {tipoConteudo} tem {FormatarTamanhoBytes(info.Length)} " +
            $"e ultrapassa {FormatarTamanhoBytes(LimiteAvisoArquivoBytes)}.");
        Console.WriteLine("Arquivos grandes podem deixar a exportação lenta ou instável.");
        Console.ResetColor();
    }

    private static Encoding ResolverEncodingSaidaExportacao(OpcoesExportacao op)
    {
        string? charset = op.Charset;
        if (string.IsNullOrWhiteSpace(charset) && op.ForcarWin1252)
            charset = "WIN1252";

        return CharsetSql.ResolverEncodingLeituraSql(charset);
    }

    private static void ValidarCombinacaoOpcoesExportacao(OpcoesExportacao op, IdiomaSaida idioma)
    {
        if (string.IsNullOrWhiteSpace(op.Tabela))
            throw new ArgumentException(
                CliText.Texto(
                    idioma,
                    "Table not provided (--table). If you are using --output with a trailing backslash in PowerShell, remove the trailing slash or escape it at the end.",
                    "Tabela nao informada (--table). Se estiver usando --output com barra final no PowerShell, remova a barra final ou use \\\\ no final."));

        if (!string.IsNullOrWhiteSpace(op.ConsultaSqlCompleta) &&
            !string.IsNullOrWhiteSpace(op.CondicaoWhere))
        {
            throw new ArgumentException(
                CliText.Texto(
                    idioma,
                    "Do not use --query-file together with --filter/--filter-file. Choose only one mode.",
                    "Não use --query-file junto com --filter/--filter-file. Escolha apenas um modo."));
        }
    }

    private static void ExibirResumoModoExportacao(OpcoesExportacao op)
    {
        string modo = string.IsNullOrWhiteSpace(op.ConsultaSqlCompleta)
            ? "Simple/Simples (--table + --filter)"
            : "Advanced/Avancado (--query-file)";

        Console.WriteLine($"Modo de consulta / Query mode: {modo}");
        Console.WriteLine($"Modo de escrita / Write mode: {op.ModoInsert}");
    }

    private static void ExibirResumoArquivosExportacao(IReadOnlyList<(string Caminho, long TamanhoBytes)> arquivosGerados)
    {
        if (arquivosGerados.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("Resumo da exportação");
        Console.WriteLine(new string('-', 72));
        Console.WriteLine($"Arquivos gerados : {arquivosGerados.Count}");
        Console.WriteLine();

        int larguraIndice = arquivosGerados.Count.ToString().Length;
        int larguraTamanho = arquivosGerados
            .Select(a => FormatarTamanhoBytes(a.TamanhoBytes).Length)
            .Max();

        for (int i = 0; i < arquivosGerados.Count; i++)
        {
            var arquivo = arquivosGerados[i];
            string tamanhoFormatado = FormatarTamanhoBytes(arquivo.TamanhoBytes).PadLeft(larguraTamanho);
            Console.WriteLine(
                $"[{(i + 1).ToString().PadLeft(larguraIndice)}] {tamanhoFormatado}  {arquivo.Caminho}");
        }

        Console.WriteLine();
        Console.WriteLine($"Arquivo final    : {arquivosGerados[^1].Caminho}");
        Console.WriteLine(new string('-', 72));
        Console.WriteLine();
    }

    private static string FormatarTamanhoBytes(long bytes)
    {
        string[] sufixos = ["B", "KB", "MB", "GB", "TB"];
        double valor = bytes;
        int indice = 0;

        while (valor >= 1024 && indice < sufixos.Length - 1)
        {
            valor /= 1024;
            indice++;
        }

        return $"{valor:0.##} {sufixos[indice]}";
    }

}
