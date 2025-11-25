using System.Text;
using SkyFBTool.Core;
using SkyFBTool.Infra;
using SkyFBTool.Services.Export;
using SkyFBTool.Services.Import;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.OutputEncoding = Encoding.UTF8;

// Mostrar ajuda se nenhum argumento for informado
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


// -----------------------------------------------------------------------------
// FUNÇÃO: EXPORTAR
// -----------------------------------------------------------------------------
static async Task ExecutarExportacao(string[] args)
{
    var op = new OpcoesExportacao();

    for (int i = 0; i < args.Length; i++)
    {
        string chave = args[i].TrimStart('-').ToLowerInvariant();

        if (i + 1 < args.Length)
        {
            string valor = args[i + 1];

            switch (chave)
            {
                case "database": op.Database = valor; break;
                case "table": op.Tabela = valor; break;
                case "alias": op.AliasTabela = valor; break;
                case "output": op.ArquivoSaida = valor; break;

                case "host": op.Host = valor; break;
                case "port": op.Porta = int.Parse(valor); break;
                case "user": op.Usuario = valor; break;
                case "password": op.Senha = valor; break;

                case "charset": op.Charset = valor; break;
                case "where": op.CondicaoWhere = valor; break;

                case "blob-format":
                    op.FormatoBlob = Enum.TryParse<FormatoBlob>(valor, true, out var fmt)
                        ? fmt : FormatoBlob.Hex;
                    break;

                case "commit-every":
                    op.CommitACada = int.Parse(valor);
                    break;

                case "progresso-cada":
                    op.ProgressoACada = int.Parse(valor);
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
    }

    Console.WriteLine("Iniciando exportação...");
    
    await using var destino = new DestinoArquivo(op.ArquivoSaida);
    await ExportadorTabelaFirebird.ExportarAsync(op, destino);

}



// -----------------------------------------------------------------------------
// FUNÇÃO: IMPORTAR
// -----------------------------------------------------------------------------
static async Task ExecutarImportacao(string[] args)
{
    var op = new OpcoesImportacao();

    for (int i = 0; i < args.Length; i++)
    {
        string chave = args[i].TrimStart('-').ToLowerInvariant();

        if (i + 1 < args.Length)
        {
            string valor = args[i + 1];

            switch (chave)
            {
                case "database": op.Database = valor; break;
                case "input": op.ArquivoEntrada = valor; break;

                case "host": op.Host = valor; break;
                case "port": op.Porta = int.Parse(valor); break;
                case "user": op.Usuario = valor; break;
                case "password": op.Senha = valor; break;

                case "progresso-cada":
                    op.ProgressoACada = int.Parse(valor);
                    break;

                case "continue-on-error":
                    op.ContinuarEmCasoDeErro = true;
                    break;
            }
        }
    }

    Console.WriteLine("Iniciando importação...");
    await ImportadorSql.ImportarAsync(op);
}



// -----------------------------------------------------------------------------
// AJUDA
// -----------------------------------------------------------------------------
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
  --output ARQUIVO.SQL        Caminho do arquivo de saída
  --charset CHARSET           WIN1252 | ISO8859_1 | UTF8 | NONE
  --blob-format FORMATO       Hex (padrão) | Base64
  --commit-every N            Insere COMMIT; a cada N linhas
  --progresso-cada N          Exibe progresso a cada N linhas
  --force-win1252             Leitura RAW em WIN1252
  --sanitize-text             Remove caracteres inválidos
  --escape-newlines           Escapa quebras de linha
  --continue-on-error         Não interrompe ao encontrar erros

EXEMPLO:
  SkyFBTool export --database C:\banco.fdb --table PESSOAS --output PESSOAS.SQL --charset WIN1252



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
  --progresso-cada N          Exibe progresso
  --continue-on-error         Continua mesmo se ocorrer erro

EXEMPLO:
  SkyFBTool import --database C:\banco.fdb --input PESSOAS.SQL

");
}
