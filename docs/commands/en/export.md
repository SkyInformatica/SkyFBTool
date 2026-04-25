# `export` command

## What it does
Exports data from a Firebird table to SQL script (`INSERT`s) using streaming.

## How to use
```powershell
SkyFBTool export --database PATH.fdb --table TABLE [options]
```

## Main options
- `--database`: database path.
- `--table`: source table.
- `--output`: output file or directory.
- `--filter` / `--filter-file`: simple filter.
- `--query-file`: full `SELECT` (do not combine with `--filter`).
- `--commit-every`: writes `COMMIT` every N rows.
- `--split-size-mb`: splits output into parts.
- `--blob-format`: `Hex` or `Base64`.

## Examples
```powershell
SkyFBTool export --database "C:\data\erp.fdb" --table CUSTOMERS --output "C:\exports\"
SkyFBTool export --database "C:\data\erp.fdb" --table ORDERS --filter "STATUS = 'A'" --output "C:\exports\orders.sql"
SkyFBTool export --database "C:\data\erp.fdb" --table ITEMS --query-file ".\sql\items.sql" --split-size-mb 200 --output "C:\exports\"
```

## Output example
```text
Query mode: Simple (--table + --filter)
File split enabled: 200 MB per file.
Starting export...

Export summary
------------------------------------------------------------------------
Files generated: 2
[1] 199.8 MB  C:\exports\ITEMS_20260425_101500_123.sql
[2]  12.4 MB  C:\exports\ITEMS_20260425_101500_123_part002.sql
Final file: C:\exports\ITEMS_20260425_101500_123_part002.sql
------------------------------------------------------------------------
```
