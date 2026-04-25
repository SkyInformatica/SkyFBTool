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

## Entradas aceitas
- `.schema.json`
- `.sql` (se houver `.schema.json` ao lado, ele é priorizado)
- `.sql` puro (parser interno)

## Exemplos
```powershell
SkyFBTool ddl-diff --source "C:\ddl\origem.schema.json" --target "C:\ddl\alvo.schema.json" --output "C:\ddl\comparacao"
SkyFBTool ddl-diff --source "C:\ddl\origem.sql" --target "C:\ddl\alvo.sql" --output "C:\ddl\comparacao_sql"
```

## Exemplo de saída
```text
Iniciando comparacao de DDL...

Comparacao concluida.
Diff SQL   : C:\ddl\comparacao.sql
Diff JSON  : C:\ddl\comparacao.json
Relatorio  : C:\ddl\comparacao.html
```
