# `import` command

## What it does
Executes SQL scripts on Firebird using streaming.

## How to use
```powershell
SkyFBTool import --database PATH.fdb --input FILE.sql [options]
```

## All options
- `--database`: database path.
- `--input`: input SQL file.
- `--script`: explicit alias for `--input`.
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--progress-every`: progress interval.
- `--continue-on-error`: keep running after SQL execution errors.

## Execution log
- A log file is always generated per execution with a unique name (`*_import_log_*.log`), including success or error status.

## Examples
```powershell
SkyFBTool import --database "C:\data\erp.fdb" --input "C:\exports\customers.sql"
SkyFBTool import --database "C:\data\erp.fdb" --script ".\sql\patch.sql" --continue-on-error
```
