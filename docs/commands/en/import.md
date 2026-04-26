# `import` command

## What it does
Executes SQL scripts on Firebird using streaming.

## How to use
```powershell
SkyFBTool import --database PATH.fdb --input FILE.sql [options]
SkyFBTool import --database PATH.fdb --inputs-batch "C:\exports\*.sql" [options]
```

## All options
- `--database`: database path.
- `--input`: input SQL file.
- `--script`: explicit alias for `--input`.
- `--inputs-batch`: wildcard pattern for SQL files.
- `--input-batch`: alias for `--inputs-batch`.
- `--scripts-batch`: alias for `--inputs-batch`.
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--progress-every`: progress interval.
- `--continue-on-error`: keep running after SQL execution errors.

Rules:
- Use only one input mode per run: `--input/--script` or `--inputs-batch`.

## Execution log
- A log file is always generated per execution with a unique name (`*_import_log_*.log`), including success or error status.

## Examples
```powershell
SkyFBTool import --database "C:\data\erp.fdb" --input "C:\exports\customers.sql"
SkyFBTool import --database "C:\data\erp.fdb" --script ".\sql\patch.sql" --continue-on-error
SkyFBTool import --database "C:\data\erp.fdb" --inputs-batch "C:\exports\*.sql" --continue-on-error
```
