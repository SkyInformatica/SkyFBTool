using System.Text;
using SkyFBTool.Cli.Commands;
using SkyFBTool.Cli.Common;

namespace SkyFBTool.Cli;

public static class CliApp
{
    public static async Task RunAsync(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.OutputEncoding = Encoding.UTF8;

        args = CliArgumentParser.NormalizarArgs(args);

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            ExibirAjuda();
            return;
        }

        string comando = args[0].ToLowerInvariant();
        string[] argsComando = args.Skip(1).ToArray();

        switch (comando)
        {
            case "export":
                await ExportCommand.ExecuteAsync(argsComando);
                break;
            case "import":
            case "exec-sql":
                await ImportCommand.ExecuteAsync(argsComando);
                break;
            case "ddl-extract":
                await DdlExtractCommand.ExecuteAsync(argsComando);
                break;
            case "ddl-diff":
                await DdlDiffCommand.ExecuteAsync(argsComando);
                break;
            case "ddl-analyze":
                await DdlAnalyzeCommand.ExecuteAsync(argsComando);
                break;
            default:
                Console.WriteLine($"Comando desconhecido: {comando}");
                ExibirAjuda();
                break;
        }
    }

    private static void ExibirAjuda()
    {
        Console.WriteLine(@"
SkyFBTool - Ferramenta de Exportacao/Importacao Firebird
--------------------------------------------------------

USO:
  SkyFBTool export   [opcoes]
  SkyFBTool import   [opcoes]
  SkyFBTool exec-sql [opcoes]
  SkyFBTool ddl-extract [opcoes]
  SkyFBTool ddl-diff    [opcoes]
  SkyFBTool ddl-analyze [opcoes]


======================
 COMANDO: EXPORT
======================

OPCOES:
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

VALIDACOES:
  --table aceita identificador simples ou entre aspas / simple or quoted identifier
  --filter pode comecar com WHERE (o prefixo e removido) / WHERE prefix is removed
  --query-file deve conter SELECT completo / must contain full SELECT
  --query-file nao pode ser usado com --filter ou --filter-file
  --filter-file/--query-file acima de 64 KB emitem aviso de desempenho/estabilidade

PADRAO DO ARQUIVO (quando --output nao for informado):
  <TABELA>_yyyyMMdd_HHmmss_fff.sql

PADRAO DE DIVISAO DE ARQUIVO:
  100 MB por arquivo (gera sufixo _part002, _part003...)
  Parte 1 mantem o nome base informado em --output
  Cada parte inicia com o mesmo cabecalho SQL (SET SQL DIALECT / SET NAMES)



======================
 COMANDO: IMPORT
======================

OPCOES:
  --database CAMINHO          Caminho do banco .fdb
  --input ARQUIVO.SQL         Arquivo SQL a importar
  --script ARQUIVO.SQL        Alias explicito de --input
  --host SERVIDOR             (padrao: localhost)
  --port PORTA                (padrao: 3050)
  --user USUARIO              (padrao: sysdba)
  --password SENHA            (padrao: masterkey)
  --progress-every N          Exibe progresso
  --continue-on-error         Continua mesmo se ocorrer erro

EXEMPLO:
  SkyFBTool import --database C:\banco.fdb --input PESSOAS.SQL
  SkyFBTool exec-sql --database C:\banco.fdb --script ajuste_schema.sql


===========================
 COMANDO: DDL-EXTRACT
===========================

OPCOES:
  --database CAMINHO          Caminho do banco .fdb
  --output CAMINHO            Prefixo/arquivo/diretorio de saida
  --host SERVIDOR             (padrao: localhost)
  --port PORTA                (padrao: 3050)
  --user USUARIO              (padrao: sysdba)
  --password SENHA            (padrao: masterkey)
  --charset CHARSET           (opcional)

SAIDA:
  <prefixo>.sql               DDL legivel
  <prefixo>.schema.json       Snapshot normalizado

EXEMPLO:
  SkyFBTool ddl-extract --database C:\banco.fdb --output C:\ddl\origem


========================
 COMANDO: DDL-DIFF
========================

OPCOES:
  --source ARQUIVO            Origem (.schema.json ou .sql gerado no extract)
  --target ARQUIVO            Alvo (.schema.json ou .sql gerado no extract)
  --output CAMINHO            Prefixo/arquivo/diretorio de saida

SAIDA:
  <prefixo>.sql               Script de ajuste do alvo para origem
  <prefixo>.json              Diferencas estruturadas
  <prefixo>.html              Relatorio visual em HTML

EXEMPLO:
  SkyFBTool ddl-diff --source C:\ddl\origem.schema.json --target C:\ddl\alvo.schema.json --output C:\ddl\comparacao


===========================
 COMANDO: DDL-ANALYZE
===========================

OPCOES:
  --input ARQUIVO            Entrada (.schema.json ou .sql do ddl-extract)
  --source ARQUIVO           Alias de --input
  --output CAMINHO           Prefixo/arquivo/diretorio de saida

SAIDA:
  <prefixo>.json             Achados estruturados
  <prefixo>.html             Relatorio visual de risco DDL

EXEMPLO:
  SkyFBTool ddl-analyze --input C:\ddl\origem.schema.json --output C:\ddl\analise

");
    }
}
