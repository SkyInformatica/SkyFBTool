# `import_batch` command

## What it does
Imports multiple SQL files into one database using wildcard pattern, executing files in sorted order.

## How to use
```powershell
SkyFBTool import_batch --database PATH.fdb --inputs-batch "C:\exports\*.sql" [options]
```

## All options
- `--database`: database path.
- `--inputs-batch`: wildcard pattern for SQL files.
- `--input-batch`: alias for `--inputs-batch`.
- `--scripts-batch`: alias for `--inputs-batch`.
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--progress-every`: progress interval.
- `--continue-on-error`: continues to next file on file-level failure.

## Examples
```powershell
SkyFBTool import_batch --database "C:\data\erp.fdb" --inputs-batch "C:\exports\*.sql"
SkyFBTool import_batch --database "C:\data\erp.fdb" --scripts-batch ".\patches\*.sql" --continue-on-error
```
