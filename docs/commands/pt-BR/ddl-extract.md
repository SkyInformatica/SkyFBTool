# Comando `ddl-extract`

## O que faz
Extrai metadados de schema Firebird e gera:
- DDL legível (`.sql`)
- snapshot normalizado (`.schema.json`)

## Como usar
```powershell
SkyFBTool ddl-extract --database CAMINHO.fdb --output PREFIXO [opções]
```

## Todas as opções
- `--database`: banco de origem.
- `--output`: prefixo, arquivo base ou diretório de saída.
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--charset`: charset opcional da conexão.

## Exemplos
```powershell
SkyFBTool ddl-extract --database "C:\dados\origem.fdb" --output "C:\ddl\origem"
SkyFBTool ddl-extract --database "C:\dados\alvo.fdb" --output "C:\ddl\alvo"
```
