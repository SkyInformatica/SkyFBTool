# `export` command

## What it does
Exports table data from Firebird to SQL script using streaming.

## How to use
```powershell
SkyFBTool export --database PATH.fdb --table TABLE [options]
```

## All options
- `--database`: database path.
- `--table`: source table.
- `--target-table`: target table name used in generated SQL.
- `--output`: output file or directory.
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--charset`: connection/output charset.
- `--filter`: simple filter expression (optional `WHERE` prefix).
- `--filter-file`: simple filter from file.
- `--query-file`: full `SELECT` from file (advanced mode).
- `--blob-format`: `Hex` or `Base64`.
- `--insert-mode`: `insert` (default) or `upsert` (`UPDATE OR INSERT ... MATCHING`).
- `--commit-every`: writes `COMMIT` every N rows.
- `--progress-every`: progress interval.
- `--split-size-mb`: split output file size in MB (`0` disables split).
- `--legacy-win1252`: forces WIN1252 behavior for legacy `CHARSET NONE`.
- `--sanitize-text`: sanitizes text values before writing SQL.
- `--escape-newlines`: escapes line breaks in text fields.
- `--continue-on-error`: keeps exporting after row write errors.

## Rules
- Do not combine `--query-file` with `--filter` or `--filter-file`.
- In `--insert-mode upsert`, PK columns must be available for `MATCHING`.

## Examples
```powershell
SkyFBTool export --database "C:\data\erp.fdb" --table CUSTOMERS --output "C:\exports\"
SkyFBTool export --database "C:\data\erp.fdb" --table ORDERS --filter "STATUS = 'A'" --output "C:\exports\orders.sql"
SkyFBTool export --database "C:\data\erp.fdb" --table ITEMS --query-file ".\sql\items.sql" --split-size-mb 200 --output "C:\exports\"
SkyFBTool export --database "C:\data\erp.fdb" --table CUSTOMERS --insert-mode upsert --escape-newlines --output "C:\exports\customers_upsert.sql"
```
