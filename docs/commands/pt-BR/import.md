# Comando `import`

## O que faz
Executa scripts SQL no Firebird em modo streaming.

## Como usar
```powershell
SkyFBTool import --database CAMINHO.fdb --input ARQUIVO.sql [opções]
SkyFBTool import --database CAMINHO.fdb --inputs-batch "C:\exports\*.sql" [opções]
```

## Todas as opções
- `--database`: caminho do banco.
- `--input`: arquivo SQL de entrada.
- `--script`: alias explícito de `--input`.
- `--inputs-batch`: padrão wildcard para arquivos SQL.
- `--input-batch`: alias de `--inputs-batch`.
- `--scripts-batch`: alias de `--inputs-batch`.
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--progress-every`: intervalo de progresso.
- `--continue-on-error`: continua após erro de execução SQL.

Regras:
- Use apenas um modo de entrada por execução: `--input/--script` ou `--inputs-batch`.

## Log de execução
- Um arquivo de log é sempre gerado por execução com nome único (`*_import_log_*.log`), indicando status de sucesso ou erro.
- No modo em lote, o resumo diferencia:
  - `Sucesso`: arquivo concluído sem erros de comandos SQL.
  - `Sucesso com erros`: arquivo concluído, mas com um ou mais comandos SQL com falha usando `--continue-on-error`.
  - `Falha`: execução do arquivo interrompida por erro fatal.

## Exemplos
```powershell
SkyFBTool import --database "C:\dados\erp.fdb" --input "C:\exports\clientes.sql"
SkyFBTool import --database "C:\dados\erp.fdb" --script ".\sql\patch.sql" --continue-on-error
SkyFBTool import --database "C:\dados\erp.fdb" --inputs-batch "C:\exports\*.sql" --continue-on-error
```
