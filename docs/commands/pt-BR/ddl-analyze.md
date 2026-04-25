# Comando `ddl-analyze`

## O que faz
Analisa risco estrutural do schema (PK, FK, índices, duplicidades, tipos desconhecidos) e gera:
- relatório estruturado (`.json`)
- relatório HTML (`.html`)

## Como usar
```powershell
SkyFBTool ddl-analyze --input ENTRADA --output PREFIXO [opções]
```

## Entradas aceitas
- `.schema.json`
- `.sql` (com ou sem `.schema.json` ao lado)

## Opções principais
- `--ignore-table-prefix`: ignora tabelas por prefixo (repetível).
- `--ignore-table-prefixes`: lista de prefixos separados por vírgula.
- `--severity-config`: arquivo JSON para sobrescrever severidade por código.

## Exemplos
```powershell
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --output "C:\ddl\analise"
SkyFBTool ddl-analyze --input "C:\ddl\origem.sql" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$ --output "C:\ddl\analise"
SkyFBTool ddl-analyze --input "C:\ddl\origem.sql" --severity-config ".\examples\ddl-severity.sample.json" --output "C:\ddl\analise_custom"
```

## Exemplo de saída
```text
Iniciando analise de DDL...

Analise concluida.
Analise JSON: C:\ddl\analise.json
Relatorio   : C:\ddl\analise.html
```
