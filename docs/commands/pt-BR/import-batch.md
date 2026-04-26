# Comando `import_batch`

## O que faz
Importa múltiplos arquivos SQL para um banco usando wildcard, executando em ordem.

## Como usar
```powershell
SkyFBTool import_batch --database CAMINHO.fdb --inputs-batch "C:\exports\*.sql" [opções]
```

## Todas as opções
- `--database`: caminho do banco.
- `--inputs-batch`: padrão wildcard para arquivos SQL.
- `--input-batch`: alias de `--inputs-batch`.
- `--scripts-batch`: alias de `--inputs-batch`.
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--progress-every`: intervalo de progresso.
- `--continue-on-error`: continua para o próximo arquivo em caso de falha de arquivo.

## Exemplos
```powershell
SkyFBTool import_batch --database "C:\dados\erp.fdb" --inputs-batch "C:\exports\*.sql"
SkyFBTool import_batch --database "C:\dados\erp.fdb" --scripts-batch ".\patches\*.sql" --continue-on-error
```
