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
                await ImportCommand.ExecuteAsync(argsComando);
                break;
            case "ddl-extract":
                await DdlExtractCommand.ExecuteAsync(argsComando);
                break;
            case "ddl-diff":
                await DdlDiffCommand.ExecuteAsync(argsComando);
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
SkyFBTool - Ferramenta de Exportação/Importação Firebird
--------------------------------------------------------

USO:
  SkyFBTool export   [opções]
  SkyFBTool import   [opções]
  SkyFBTool ddl-extract [opções]
  SkyFBTool ddl-diff    [opções]


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
  --filter-file/--query-file acima de 64 KB emitem aviso de desempenho/estabilidade

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


===========================
 COMANDO: DDL-EXTRACT
===========================

OPÇÕES:
  --database CAMINHO          Caminho do banco .fdb
  --output CAMINHO            Prefixo/arquivo/diretório de saída
  --host SERVIDOR             (padrão: localhost)
  --port PORTA                (padrão: 3050)
  --user USUARIO              (padrão: sysdba)
  --password SENHA            (padrão: masterkey)
  --charset CHARSET           (opcional)

SAÍDA:
  <prefixo>.sql               DDL legível
  <prefixo>.schema.json       Snapshot normalizado

EXEMPLO:
  SkyFBTool ddl-extract --database C:\banco.fdb --output C:\ddl\origem


========================
 COMANDO: DDL-DIFF
========================

OPÇÕES:
  --source ARQUIVO            Origem (.schema.json ou .sql gerado no extract)
  --target ARQUIVO            Alvo (.schema.json ou .sql gerado no extract)
  --output CAMINHO            Prefixo/arquivo/diretório de saída

SAÍDA:
  <prefixo>.sql               Script de ajuste do alvo para origem
  <prefixo>.json              Diferenças estruturadas
  <prefixo>.md                Relatório de diferenças

EXEMPLO:
  SkyFBTool ddl-diff --source C:\ddl\origem.schema.json --target C:\ddl\alvo.schema.json --output C:\ddl\comparacao

");
    }
}
