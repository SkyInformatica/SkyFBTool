# Comando `exec-sql`

## O que faz
Alias de `import`, usado normalmente para scripts de manutenção/patch.

## Como usar
```powershell
SkyFBTool exec-sql --database CAMINHO.fdb --script ARQUIVO.sql [opções]
```

## Todas as opções
`exec-sql` usa o mesmo parser/opções de `import`:
- `--database`
- `--input`
- `--script` (alias explícito de `--input`)
- `--host`
- `--port`
- `--user`
- `--password`
- `--progress-every`
- `--continue-on-error`

## Exemplos
```powershell
SkyFBTool exec-sql --database "C:\dados\erp.fdb" --script ".\sql\patch_2026_04.sql"
SkyFBTool exec-sql --database "C:\dados\erp.fdb" --script ".\sql\rebuild_indexes.sql" --continue-on-error
```
