# Comando `ddl-analyze`

## O que faz
Analisa risco estrutural do schema (PK, FK, índices, duplicidades, tipos desconhecidos) e gera:
- relatório estruturado (`.json`)
- relatório HTML (`.html`)
- no modo lote, resumo consolidado para DBA (`batch_analysis_summary_*.json` e `.html`)

## Como usar
```powershell
SkyFBTool ddl-analyze --input ENTRADA --output PREFIXO [opções]
SkyFBTool ddl-analyze --database CAMINHO.fdb --output PREFIXO [opções]
SkyFBTool ddl-analyze --databases-batch "C:\dados\*.fdb" --output DIRETORIO [opções]
```

## Entradas aceitas
- `.schema.json`
- `.sql` (com ou sem `.schema.json` ao lado)
- conexão direta no banco (`--database`)
- wildcard de bancos em lote (`--databases-batch`)

## Opções principais
- `--database`, `--host`, `--port`, `--user`, `--password`, `--charset`: origem por conexão no banco.
- `--databases-batch`: padrão wildcard (`*`, `?`) para análise em lote.
- `--ignore-table-prefix`: ignora tabelas por prefixo (repetível).
- `--ignore-table-prefixes`: lista de prefixos separados por vírgula.
- `--severity-config`: arquivo JSON para sobrescrever severidade por código.

## Exemplos
```powershell
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --output "C:\ddl\analise"
SkyFBTool ddl-analyze --input "C:\ddl\origem.sql" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$ --output "C:\ddl\analise"
SkyFBTool ddl-analyze --database "C:\dados\origem.fdb" --output "C:\ddl\analise_do_banco"
SkyFBTool ddl-analyze --databases-batch "C:\dados\*.fdb" --output "C:\ddl\analises_lote\"
SkyFBTool ddl-analyze --input "C:\ddl\origem.sql" --severity-config ".\docs\examples\ddl-severity.sample.json" --output "C:\ddl\analise_custom"
```

## Exemplo de saída
```text
Iniciando análise de DDL...

Análise em lote concluída.
Resumo do lote JSON     : C:\ddl\analises_lote\batch_analysis_summary_20260426_103000_123.json
Relatório resumo do lote: C:\ddl\analises_lote\batch_analysis_summary_20260426_103000_123.html
```
