namespace SkyFBTool.Cli.Common;

public static class CliHelpText
{
    public static string ObterTextoCompleto()
    {
        return @"
SkyFBTool - CLI Firebird para exportação, importação, execução de scripts, extração, diff e análise DDL
--------------------------------------------------------

USO:
  SkyFBTool export   [opções]
  SkyFBTool import   [opções]
  SkyFBTool exec-sql [opções]
  SkyFBTool ddl-extract [opções]
  SkyFBTool ddl-diff    [opções]
  SkyFBTool ddl-analyze [opções]


======================
 COMANDO: EXPORT
======================

OPÇÕES:
  --database CAMINHO          Caminho do banco .fdb / Database path
  --table TABELA              Tabela de origem / Source table
  --target-table NOME         Tabela destino no INSERT / Target table in INSERT
  --output ARQUIVO.SQL        Saída SQL (arquivo ou diretório) / SQL output (file or directory)
  --charset CHARSET           WIN1252 | ISO8859_1 | UTF8 | NONE
  --blob-format FORMATO       Hex (padrão) | Base64
  --insert-mode MODO          Insert (padrão) | Upsert (UPDATE OR INSERT ... MATCHING PK)
  --commit-every N            COMMIT a cada N linhas / COMMIT every N rows
  --split-size-mb N           Divide em partes de N MB (padrão: 100; 0 desativa)
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
  --query-file não pode ser usado com --filter ou --filter-file
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
  --script ARQUIVO.SQL        Alias explícito de --input
  --inputs-batch PADRAO       Wildcard de arquivos SQL (ex.: C:\exports\*.sql)
  --input-batch PADRAO        Alias de --inputs-batch
  --scripts-batch PADRAO      Alias de --inputs-batch
  --host SERVIDOR             (padrão: localhost)
  --port PORTA                (padrão: 3050)
  --user USUARIO              (padrão: sysdba)
  --password SENHA            (padrão: masterkey)
  --progress-every N          Exibe progresso
  --continue-on-error         Continua mesmo se ocorrer erro

EXEMPLO:
  SkyFBTool import --database C:\banco.fdb --input PESSOAS.SQL
  SkyFBTool import --database C:\banco.fdb --inputs-batch C:\exports\*.sql --continue-on-error
  SkyFBTool exec-sql --database C:\banco.fdb --script ajuste_schema.sql


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
  <prefixo>.html              Relatório visual em HTML

EXEMPLO:
  SkyFBTool ddl-diff --source C:\ddl\origem.schema.json --target C:\ddl\alvo.schema.json --output C:\ddl\comparacao


===========================
 COMANDO: DDL-ANALYZE
===========================

OPÇÕES:
  --input ARQUIVO            Entrada por arquivo (.schema.json ou .sql)
  --source ARQUIVO           Alias de --input
  --database CAMINHO         Entrada por conexão direta em banco único
  --databases-batch PADRAO   Entrada em lote por wildcard (ex.: C:\dados\*.fdb)
  --host SERVIDOR            (padrão: localhost)
  --port PORTA               (padrão: 3050)
  --user USUARIO             (padrão: sysdba)
  --password SENHA           (padrão: masterkey)
  --charset CHARSET          (opcional)
  --output CAMINHO           Prefixo/arquivo/diretório de saída
  --ignore-table-prefix TXT  Ignora tabelas por prefixo (pode repetir)
  --ignore-table-prefixes L  Ignora prefixos separados por vírgula
  --severity-config ARQ.JSON Sobrescreve severidade por código de achado
  --description TEXTO        Description text included in JSON/HTML report
  --volume-analysis MODO     on (padrão) | off
  --volume-count-exact MODO  on | off (padrão: off; on executa COUNT(*) por tabela)

SAÍDA:
  <prefixo>.json             Achados estruturados
  <prefixo>.html             Relatório visual de risco DDL
  (modo lote)                batch_analysis_summary_*.json/.html

EXEMPLO:
  SkyFBTool ddl-analyze --input C:\ddl\origem.schema.json --output C:\ddl\analise
  SkyFBTool ddl-analyze --input C:\ddl\origem.schema.json --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$
  SkyFBTool ddl-analyze --database C:\dados\origem.fdb --output C:\ddl\analise_db
  SkyFBTool ddl-analyze --databases-batch C:\dados\*.fdb --output C:\ddl\analises\
  SkyFBTool ddl-analyze --input C:\ddl\origem.schema.json --severity-config .\docs\examples\ddl-severity.sample.json
  SkyFBTool ddl-analyze --input C:\ddl\origem.schema.json --description ""Analysis performed on customer Ubirici database""

";
    }
}
