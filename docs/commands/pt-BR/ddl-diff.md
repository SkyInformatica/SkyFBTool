# Comando `ddl-diff`

## O que faz
Compara dois schemas e gera:
- SQL de ajuste (`.sql`)
- diff estruturado (`.json`)
- relatório visual (`.html`)

## Como usar
```powershell
SkyFBTool ddl-diff --source ORIGEM --target ALVO --output PREFIXO
```

## Todas as opções
- `--source`: entrada de origem (`.schema.json` ou `.sql`).
- `--source-ddl`: alias de `--source`.
- `--target`: entrada de alvo (`.schema.json` ou `.sql`).
- `--target-ddl`: alias de `--target`.
- `--output`: prefixo/arquivo base/diretório de saída.

## Exemplos
```powershell
SkyFBTool ddl-diff --source "C:\ddl\origem.schema.json" --target "C:\ddl\alvo.schema.json" --output "C:\ddl\comparacao"
SkyFBTool ddl-diff --source-ddl "C:\ddl\origem.sql" --target-ddl "C:\ddl\alvo.sql" --output "C:\ddl\comparacao_sql"
```
