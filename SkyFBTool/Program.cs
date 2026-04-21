using System.Text;
using SkyFBTool.Core;
using SkyFBTool.Infra;
using SkyFBTool.Services.Export;
using SkyFBTool.Services.Import;

const long LimiteTecnicoWhereArquivoBytes = 100L * 1024 * 1024;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    ExibirAjuda();
    return;
}

string comando = args[0].ToLowerInvariant();

if (comando == "export")
{
    await ExecutarExportacao(args.Skip(1).ToArray());
}
else if (comando == "import")
{
    await ExecutarImportacao(args.Skip(1).ToArray());
}
else
{
    Console.WriteLine($"Comando desconhecido: {comando}");
    ExibirAjuda();
}
return;


static async Task ExecutarExportacao(string[] args)
{
    var op = new OpcoesExportacao();

    for (int i = 0; i < args.Length; i++)
    {
        string chave = args[i].TrimStart('-').ToLowerInvariant();

        switch (chave)
        {
            case "database":
                op.Database = LerValorOpcao(args, ref i, chave);
                break;
            case "table":
                op.Tabela = LerValorOpcao(args, ref i, chave);
                break;
            case "target-table":
                op.AliasTabela = LerValorOpcao(args, ref i, chave);
                break;
            case "output":
                op.ArquivoSaida = LerValorOpcao(args, ref i, chave);
                break;
            case "host":
                op.Host = LerValorOpcao(args, ref i, chave);
                break;
            case "port":
                op.Porta = int.Parse(LerValorOpcao(args, ref i, chave));
                break;
            case "user":
                op.Usuario = LerValorOpcao(args, ref i, chave);
                break;
            case "password":
                op.Senha = LerValorOpcao(args, ref i, chave);
                break;
            case "charset":
                op.Charset = LerValorOpcao(args, ref i, chave);
                break;
            case "filter":
                op.CondicaoWhere = ResolverCondicaoWhere(LerValorOpcao(args, ref i, chave));
                break;
            case "filter-file":
                op.CondicaoWhere = LerCondicaoWhereDeArquivo(LerValorOpcao(args, ref i, chave));
                break;
            case "query-file":
                op.ConsultaSqlCompleta = LerConsultaSqlDeArquivo(LerValorOpcao(args, ref i, chave));
                break;
            case "blob-format":
                var valorBlobFormat = LerValorOpcao(args, ref i, chave);
                op.FormatoBlob = Enum.TryParse<FormatoBlob>(valorBlobFormat, true, out var fmt)
                    ? fmt
                    : FormatoBlob.Hex;
                break;
            case "commit-every":
                op.CommitACada = int.Parse(LerValorOpcao(args, ref i, chave));
                break;
            case "progress-every":
                op.ProgressoACada = int.Parse(LerValorOpcao(args, ref i, chave));
                break;
            case "split-size-mb":
                op.TamanhoMaximoArquivoMb = int.Parse(LerValorOpcao(args, ref i, chave));
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
        }
    }

    ValidarCombinacaoOpcoesExportacao(op);
    ExibirResumoModoExportacao(op);

    var arquivoSaidaInformado = op.ArquivoSaida;
    op.ArquivoSaida = ResolverArquivoSaidaExportacao(op);

    if (string.IsNullOrWhiteSpace(arquivoSaidaInformado))
    {
        Console.WriteLine($"Arquivo de saída não informado. Usando: {op.ArquivoSaida}");
    }
    else if (EhDiretorio(arquivoSaidaInformado))
    {
        Console.WriteLine($"Diretório informado em --output. Arquivo gerado: {op.ArquivoSaida}");
    }

    if (op.TamanhoMaximoArquivoMb > 0)
    {
        Console.WriteLine($"Divisão de arquivo ativa: {op.TamanhoMaximoArquivoMb} MB por arquivo.");
    }
    else
    {
        Console.WriteLine("Divisão de arquivo desativada.");
    }

    Console.WriteLine("Iniciando exportação...");

    var encodingSaida = ResolverEncodingSaidaExportacao(op);
    await using var destino = new DestinoArquivo(op.ArquivoSaida, op.TamanhoMaximoArquivoMb, encodingSaida);
    await ExportadorTabelaFirebird.ExportarAsync(op, destino);
    ExibirResumoArquivosExportacao(destino.ObterArquivosGerados());

}



static async Task ExecutarImportacao(string[] args)
{
    var op = new OpcoesImportacao();

    for (int i = 0; i < args.Length; i++)
    {
        string chave = args[i].TrimStart('-').ToLowerInvariant();

        switch (chave)
        {
            case "database":
                op.Database = LerValorOpcao(args, ref i, chave);
                break;
            case "input":
                op.ArquivoEntrada = LerValorOpcao(args, ref i, chave);
                break;
            case "host":
                op.Host = LerValorOpcao(args, ref i, chave);
                break;
            case "port":
                op.Porta = int.Parse(LerValorOpcao(args, ref i, chave));
                break;
            case "user":
                op.Usuario = LerValorOpcao(args, ref i, chave);
                break;
            case "password":
                op.Senha = LerValorOpcao(args, ref i, chave);
                break;
            case "progress-every":
                op.ProgressoACada = int.Parse(LerValorOpcao(args, ref i, chave));
                break;
            case "continue-on-error":
                op.ContinuarEmCasoDeErro = true;
                break;
        }
    }

    Console.WriteLine("Iniciando importação...");
    await ImportadorSql.ImportarAsync(op);
}



static void ExibirAjuda()
{
    Console.WriteLine(@"
SkyFBTool - Ferramenta de Exportação/Importação Firebird
--------------------------------------------------------

USO:
  SkyFBTool export   [opções]
  SkyFBTool import   [opções]


======================
 COMANDO: EXPORT
======================

OPÇÕES:
  --database CAMINHO          Caminho do banco .fdb / Database path
  --table TABELA              Tabela de origem / Source table
  --target-table NOME         Tabela destino no INSERT / Target table in INSERT
  --output ARQUIVO.SQL        Saida SQL (arquivo ou diretorio) / SQL output (file or directory)
  --charset CHARSET           WIN1252 | ISO8859_1 | UTF8 | NONE
  --blob-format FORMATO       Hex (padrao) | Base64
  --commit-every N            COMMIT a cada N linhas / COMMIT every N rows
  --split-size-mb N           Divide em partes de N MB (padrao: 100; 0 desativa)
  --progress-every N          Exibe progresso / Progress interval
  --legacy-win1252            Modo legado WIN1252 para bases NONE / Legacy mode for NONE
  --sanitize-text             Sanitiza texto / Sanitize text
  --escape-newlines           Escapa quebras de linha / Escape newlines
  --filter CONDICAO           Filtro simples inline ou arquivo / Simple inline filter or file
  --filter-file CAMINHO       Filtro simples em arquivo / Simple filter from file
  --query-file CAMINHO        SELECT completo em arquivo / Full SELECT from file
  --continue-on-error         Continua em erro / Continue on error

EXEMPLO:
  SkyFBTool export --database C:\banco.fdb --table PESSOAS --output PESSOAS.SQL --charset WIN1252

VALIDAÇÕES:
  --table aceita identificador simples ou entre aspas / simple or quoted identifier
  --filter pode começar com WHERE (o prefixo é removido) / WHERE prefix is removed
  --query-file deve conter SELECT completo / must contain full SELECT
  --query-file nao pode ser usado com --filter ou --filter-file

PADRÃO DO ARQUIVO (quando --output não for informado):
  <TABELA>_yyyyMMdd_HHmmss_fff.sql

PADRÃO DE DIVISÃO DE ARQUIVO:
  100 MB por arquivo (gera sufixo _part002, _part003...)
  Parte 1 mantém o nome base informado em --output
  Cada parte inicia com o mesmo cabeçalho SQL (SET SQL DIALECT / SET NAMES)



======================
 COMANDO: IMPORT
======================

OPÇÕES:
  --database CAMINHO          Caminho do banco .fdb
  --input ARQUIVO.SQL         Arquivo SQL a importar
  --host SERVIDOR             (padrão: localhost)
  --port PORTA                (padrão: 3050)
  --user USUARIO              (padrão: sysdba)
  --password SENHA            (padrão: masterkey)
  --progress-every N          Exibe progresso
  --continue-on-error         Continua mesmo se ocorrer erro

EXEMPLO:
  SkyFBTool import --database C:\banco.fdb --input PESSOAS.SQL

");
}

static string GerarNomeArquivoExportacao(OpcoesExportacao op)
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

static string ResolverArquivoSaidaExportacao(OpcoesExportacao op)
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

static bool EhDiretorio(string caminho)
{
    if (Directory.Exists(caminho))
        return true;

    return caminho.EndsWith(Path.DirectorySeparatorChar)
           || caminho.EndsWith(Path.AltDirectorySeparatorChar);
}

static string LerValorOpcao(string[] args, ref int indiceAtual, string chave)
{
    int proximoIndice = indiceAtual + 1;
    if (proximoIndice >= args.Length)
        throw new ArgumentException($"Valor não informado para --{chave}.");

    string proximoValor = args[proximoIndice];
    if (proximoValor.StartsWith("-"))
        throw new ArgumentException($"Valor inválido para --{chave}: {proximoValor}");

    indiceAtual = proximoIndice;
    return proximoValor;
}

static string? ResolverCondicaoWhere(string valor)
{
    string candidato = valor.Trim();

    if (File.Exists(candidato))
        return LerCondicaoWhereDeArquivo(candidato);

    return candidato;
}

static string LerCondicaoWhereDeArquivo(string caminhoArquivo)
{
    if (!File.Exists(caminhoArquivo))
        throw new FileNotFoundException($"Arquivo de condição WHERE não encontrado: {caminhoArquivo}");

    var info = new FileInfo(caminhoArquivo);
    if (info.Length > LimiteTecnicoWhereArquivoBytes)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(
            $"Aviso: o arquivo de condição WHERE tem {FormatarTamanhoBytes(info.Length)} " +
            $"e excede o limite técnico recomendado de {FormatarTamanhoBytes(LimiteTecnicoWhereArquivoBytes)}.");
        Console.WriteLine("Isso pode deixar a exportação lenta ou travar o processo.");
        Console.ResetColor();
    }

    string conteudo = File.ReadAllText(caminhoArquivo).Trim();
    if (string.IsNullOrWhiteSpace(conteudo))
        throw new ArgumentException($"Arquivo de condição WHERE vazio: {caminhoArquivo}");

    return conteudo;
}

static string LerConsultaSqlDeArquivo(string caminhoArquivo)
{
    if (!File.Exists(caminhoArquivo))
        throw new FileNotFoundException($"Arquivo de consulta SQL não encontrado: {caminhoArquivo}");

    string conteudo = File.ReadAllText(caminhoArquivo).Trim();
    if (string.IsNullOrWhiteSpace(conteudo))
        throw new ArgumentException($"Arquivo de consulta SQL vazio: {caminhoArquivo}");

    return conteudo;
}

static Encoding ResolverEncodingSaidaExportacao(OpcoesExportacao op)
{
    string? charset = op.Charset;
    if (string.IsNullOrWhiteSpace(charset) && op.ForcarWin1252)
        charset = "WIN1252";

    return CharsetSql.ResolverEncodingLeituraSql(charset);
}

static void ValidarCombinacaoOpcoesExportacao(OpcoesExportacao op)
{
    if (string.IsNullOrWhiteSpace(op.Tabela))
        throw new ArgumentException("Tabela nao informada (--table).");

    if (!string.IsNullOrWhiteSpace(op.ConsultaSqlCompleta) &&
        !string.IsNullOrWhiteSpace(op.CondicaoWhere))
    {
        throw new ArgumentException(
            "Nao use --query-file junto com --filter/--filter-file. Escolha apenas um modo.");
    }
}

static void ExibirResumoModoExportacao(OpcoesExportacao op)
{
    string modo = string.IsNullOrWhiteSpace(op.ConsultaSqlCompleta)
        ? "Simple/Simples (--table + --filter)"
        : "Advanced/Avancado (--query-file)";

    Console.WriteLine($"Modo de consulta / Query mode: {modo}");
}

static void ExibirResumoArquivosExportacao(IReadOnlyList<(string Caminho, long TamanhoBytes)> arquivosGerados)
{
    if (arquivosGerados.Count == 0)
        return;

    Console.WriteLine();
    Console.WriteLine($"Arquivos gerados: {arquivosGerados.Count}");

    for (int i = 0; i < arquivosGerados.Count; i++)
    {
        var arquivo = arquivosGerados[i];
        Console.WriteLine(
            $"[{i + 1}] {arquivo.Caminho} ({FormatarTamanhoBytes(arquivo.TamanhoBytes)})");
    }

    Console.WriteLine($"Arquivo final: {arquivosGerados[^1].Caminho}");
}

static string FormatarTamanhoBytes(long bytes)
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
