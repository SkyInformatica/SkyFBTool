using System.Text;
using SkyFBTool.Core;
using SkyFBTool.Infra;
using SkyFBTool.Services.Export;
using SkyFBTool.Services.Import;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    ExibirAjuda();
    return;
}

string comando = args[0].ToLowerInvariant();

if (comando == "export" || comando == "exportar")
{
    await ExecutarExportacao(args.Skip(1).ToArray());
}
else if (comando == "import" || comando == "importar")
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
            case "alias":
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
            case "where":
                op.CondicaoWhere = LerValorOpcao(args, ref i, chave);
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
            case "max-file-size-mb":
            case "split-size-mb":
                op.TamanhoMaximoArquivoMb = int.Parse(LerValorOpcao(args, ref i, chave));
                break;
            case "force-win1252":
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
  --database CAMINHO          Caminho do banco .fdb
  --table TABELA              Nome da tabela a exportar
  --alias NOVO_NOME           Nome alternativo da tabela no arquivo SQL
  --output ARQUIVO.SQL        Caminho do arquivo de saída (opcional; aceita diretório)
  --charset CHARSET           WIN1252 | ISO8859_1 | UTF8 | NONE
  --blob-format FORMATO       Hex (padrão) | Base64
  --commit-every N            Insere COMMIT; a cada N linhas
  --max-file-size-mb N        Divide o SQL em partes de até N MB (padrão: 100; 0 desativa)
  --progress-every N          Exibe progresso a cada N linhas
  --force-win1252             Leitura RAW em WIN1252
  --sanitize-text             Remove caracteres inválidos
  --escape-newlines           Escapa quebras de linha
  --where CONDICAO            Condição WHERE (opcional; sem ';', '--', '/*' ou '*/')
  --continue-on-error         Não interrompe ao encontrar erros

EXEMPLO:
  SkyFBTool export --database C:\banco.fdb --table PESSOAS --output PESSOAS.SQL --charset WIN1252

VALIDAÇÕES:
  --table aceita identificador simples ou entre aspas
  --where pode começar com WHERE (o prefixo é removido automaticamente)

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

static Encoding ResolverEncodingSaidaExportacao(OpcoesExportacao op)
{
    string? charset = op.Charset;
    if (string.IsNullOrWhiteSpace(charset) && op.ForcarWin1252)
        charset = "WIN1252";

    return CharsetSql.ResolverEncodingLeituraSql(charset);
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
