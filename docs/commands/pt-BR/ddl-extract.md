# Comando `ddl-extract`

## O que faz
Extrai o schema do banco Firebird e gera dois artefatos:
- DDL legível (`.sql`)
- snapshot estruturado (`.schema.json`)

## Como usar
```powershell
SkyFBTool ddl-extract --database CAMINHO.fdb --output PREFIXO [opções]
```

## Opções principais
- `--database`: banco de origem.
- `--output`: prefixo, arquivo-base ou diretório de saída.
- `--charset`: charset opcional para conexão.

## Exemplos
```powershell
SkyFBTool ddl-extract --database "C:\dados\origem.fdb" --output "C:\ddl\origem"
SkyFBTool ddl-extract --database "C:\dados\alvo.fdb" --output "C:\ddl\alvo"
```

## Exemplo de saída
```text
Iniciando extração de DDL...

Extração concluída.
DDL SQL    : C:\ddl\origem.sql
Schema JSON: C:\ddl\origem.schema.json
```
