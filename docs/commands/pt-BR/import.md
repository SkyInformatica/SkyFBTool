# Comando `import`

## O que faz
Executa scripts SQL no Firebird em modo streaming.

## Como usar
```powershell
SkyFBTool import --database CAMINHO.fdb --input ARQUIVO.sql [opções]
```

## Todas as opções
- `--database`: caminho do banco.
- `--input`: arquivo SQL de entrada.
- `--script`: alias explícito de `--input`.
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--progress-every`: intervalo de progresso.
- `--continue-on-error`: continua após erro de execução SQL.

## Log de execução
- Um arquivo de log é sempre gerado por execução com nome único (`*_import_log_*.log`), indicando status de sucesso ou erro.

## Exemplos
```powershell
SkyFBTool import --database "C:\dados\erp.fdb" --input "C:\exports\clientes.sql"
SkyFBTool import --database "C:\dados\erp.fdb" --script ".\sql\patch.sql" --continue-on-error
```
