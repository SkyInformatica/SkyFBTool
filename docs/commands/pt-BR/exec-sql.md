# Comando `exec-sql`

## O que faz
É um alias de `import`, usado para executar scripts SQL de ajuste (schema, índices, correções).

## Como usar
```powershell
SkyFBTool exec-sql --database CAMINHO.fdb --script ARQUIVO.sql [opções]
```

## Opções principais
- `--script`: alias explícito de `--input`.
- Aceita as mesmas opções de `import` (`--progress-every`, `--continue-on-error`, etc.).

## Exemplos
```powershell
SkyFBTool exec-sql --database "C:\dados\erp.fdb" --script ".\sql\patch_2026_04.sql"
SkyFBTool exec-sql --database "C:\dados\erp.fdb" --script ".\sql\rebuild_indexes.sql" --continue-on-error
```

## Exemplo de saída
```text
Iniciando importação...
Importação concluída.
Total de comandos executados: 182
```
