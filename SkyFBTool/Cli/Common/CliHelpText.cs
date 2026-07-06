namespace SkyFBTool.Cli.Common;

public static class CliHelpText
{
    public static string ObterResumoComandos()
    {
        return @"
SkyFBTool - CLI Firebird
------------------------

USO:
  SkyFBTool <comando> [opções]
  SkyFBTool <comando> --help

COMANDOS:
  export
  create-db
  import
  exec-sql
  ddl-extract
  ddl-diff
  ddl-analyze

EXEMPLOS:
  SkyFBTool export --help
  SkyFBTool ddl-analyze --help
";
    }

    public static string? ObterAjudaComando(string comando)
    {
        return comando.ToLowerInvariant() switch
        {
            "export" => AjudaExport,
            "create-db" => AjudaCreateDb,
            "import" => AjudaImport,
            "exec-sql" => AjudaImport,
            "ddl-extract" => AjudaDdlExtract,
            "ddl-diff" => AjudaDdlDiff,
            "ddl-analyze" => AjudaDdlAnalyze,
            _ => null
        };
    }

    private const string AjudaExport = @"
COMANDO: export

OPÇÕES:
  --database CAMINHO
  --table TABELA
  --target-table NOME
  --output ARQUIVO.SQL
  --charset CHARSET           WIN1252 | ISO8859_1 | UTF8 | NONE
  --blob-format FORMATO       Hex (padrão) | Base64
  --insert-mode MODO          Insert (padrão) | Upsert
  --commit-every N
  --split-size-mb N
  --progress-every N
  --legacy-win1252
  --sanitize-text
  --escape-newlines
  --filter CONDICAO
  --filter-file CAMINHO
  --query-file CAMINHO
  --continue-on-error

EXEMPLO:
  SkyFBTool export --database C:\banco.fdb --table PESSOAS --output PESSOAS.SQL
";

    private const string AjudaCreateDb = @"
COMANDO: create-db

OPÇÕES:
  --database CAMINHO          (obrigatório)
  --host SERVIDOR             (padrão: localhost)
  --port PORTA                (padrão: 3050)
  --user USUARIO              (padrão: sysdba)
  --password SENHA            (padrão: masterkey)
  --charset CHARSET           (padrão: UTF8)
  --page-size N               (padrão: 8192)
  --forced-writes MODO        on (padrão) | off
  --overwrite                 recria arquivo existente
  --ddl-file ARQUIVO.SQL      aplica script após criar

EXEMPLO:
  SkyFBTool create-db --database C:\dados\novo.fdb --ddl-file C:\ddl\estrutura.sql
";

    private const string AjudaImport = @"
COMANDO: import / exec-sql

OPÇÕES:
  --database CAMINHO
  --input ARQUIVO.SQL
  --script ARQUIVO.SQL
  --inputs-batch PADRAO
  --input-batch PADRAO
  --scripts-batch PADRAO
  --host SERVIDOR             (padrão: localhost)
  --port PORTA                (padrão: 3050)
  --user USUARIO              (padrão: sysdba)
  --password SENHA            (padrão: masterkey)
  --progress-every N
  --continue-on-error

EXEMPLO:
  SkyFBTool import --database C:\banco.fdb --input PESSOAS.SQL
";

    private const string AjudaDdlExtract = @"
COMANDO: ddl-extract

OPÇÕES:
  --database CAMINHO
  --output CAMINHO
  --host SERVIDOR             (padrão: localhost)
  --port PORTA                (padrão: 3050)
  --user USUARIO              (padrão: sysdba)
  --password SENHA            (padrão: masterkey)
  --charset CHARSET

SAÍDA:
  <prefixo>.sql
  <prefixo>.schema.json

EXEMPLO:
  SkyFBTool ddl-extract --database C:\banco.fdb --output C:\ddl\origem
";

    private const string AjudaDdlDiff = @"
COMANDO: ddl-diff

OPÇÕES:
  --source ARQUIVO
  --target ARQUIVO
  --output CAMINHO
  --include-domains

SAÍDA:
  <prefixo>.sql
  <prefixo>.json
  <prefixo>.html

EXEMPLO:
  SkyFBTool ddl-diff --source C:\ddl\origem.schema.json --target C:\ddl\alvo.schema.json --output C:\ddl\comparacao
";

    private const string AjudaDdlAnalyze = @"
COMANDO: ddl-analyze

OPÇÕES:
  --input ARQUIVO
  --source ARQUIVO
  --database CAMINHO
  --databases-batch PADRAO
  --host SERVIDOR             (padrão: localhost)
  --port PORTA                (padrão: 3050)
  --user USUARIO              (padrão: sysdba)
  --password SENHA            (padrão: masterkey)
  --charset CHARSET
  --output CAMINHO
  --ignore-table-prefix TXT
  --ignore-table-prefixes L
  --severity-config ARQ.JSON
  --description TEXTO

SAÍDA:
  <prefixo>.json
  <prefixo>.html
  (modo lote) <banco>_schema_analysis_<timestamp>.json/.html
  (modo lote) batch_analysis_summary_*.json/.html

EXEMPLO:
  SkyFBTool ddl-analyze --input C:\ddl\origem.schema.json --output C:\ddl\analise
";
}
