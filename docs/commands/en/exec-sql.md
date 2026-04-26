# `exec-sql` command

## What it does
Alias for `import`, commonly used for maintenance/patch SQL scripts.

## How to use
```powershell
SkyFBTool exec-sql --database PATH.fdb --script FILE.sql [options]
```

## All options
`exec-sql` uses the same parser/options as `import`:
- `--database`
- `--input`
- `--script` (explicit alias for `--input`)
- `--host`
- `--port`
- `--user`
- `--password`
- `--progress-every`
- `--continue-on-error`

## Examples
```powershell
SkyFBTool exec-sql --database "C:\data\erp.fdb" --script ".\sql\patch_2026_04.sql"
SkyFBTool exec-sql --database "C:\data\erp.fdb" --script ".\sql\rebuild_indexes.sql" --continue-on-error
```
