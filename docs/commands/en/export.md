# `export` command

## What it does
Exports table data from Firebird to SQL script using streaming.

## How to use
```powershell
SkyFBTool export --database PATH.fdb --table TABLE [options]
```

## All options
- `--database`: Firebird database file/path to read from.
- `--table`: source table name used in the default generated `SELECT`.
- `--target-table`: changes only the table name written in generated `INSERT`/`UPSERT`; useful when exporting from one table and importing into another with compatible schema.
- `--output`: output file or directory. If directory is provided, the tool generates a timestamped file name.
- `--host`: Firebird server host (default: `localhost`).
- `--port`: Firebird server port (default: `3050`).
- `--user`: Firebird user (default: `sysdba`).
- `--password`: Firebird password (default: `masterkey`).
- `--charset`: charset used for DB connection and SQL script header (`SET NAMES`); use it to keep accent/special-character fidelity.
- `--filter`: simple `WHERE` condition appended to table export query; supports optional `WHERE` prefix.
- `--filter-file`: same behavior as `--filter`, but reads condition text from file (better for long conditions).
- `--query-file`: full custom `SELECT` (advanced mode). Use when you need joins, expressions, ordering, or explicit column projection.
- `--blob-format`: BLOB serialization format in SQL literals: `Hex` (default, more portable) or `Base64` (usually smaller output).
- `--insert-mode`: `insert` (plain `INSERT`) or `upsert` (`UPDATE OR INSERT ... MATCHING`); `upsert` is safer for idempotent reimports but depends on PK matching columns.
- `--commit-every`: writes `COMMIT` every N generated rows; helps reduce long transactions during import.
- `--progress-every`: prints export progress every N rows; does not change output SQL, only console observability.
- `--split-size-mb`: splits output into multiple files when the size threshold is reached; each part has SQL header. Use `0` to disable split.
- `--legacy-win1252`: forces legacy WIN1252 reader strategy for `CHARSET NONE` databases/files with old encoding behavior.
- `--sanitize-text`: normalizes problematic control characters in text fields to reduce import/parser issues downstream.
- `--escape-newlines`: converts line breaks in text values to escaped sequences, improving single-line SQL readability and parser stability.
- `--continue-on-error`: continues export after row-level write/serialization errors and logs failures instead of aborting the entire run.

## Rules
- Input mode and query composition
  - Do not combine `--query-file` with `--filter` or `--filter-file`.
  - Use `--table` as the default mode (simple table export); use `--query-file` only when you need full SQL control.
  - `--filter` and `--filter-file` are intended for simple predicates; they are appended to generated table query.

- Consistency between source and generated SQL
  - If `--target-table` is used, ensure destination table schema is compatible with exported columns/order.
  - In `--insert-mode upsert`, PK (or matching key) columns must exist in exported projection for `MATCHING`.
  - If you customize projection in `--query-file`, keep column semantics compatible with import target.

- Encoding and text safety
  - Prefer explicit `--charset` to avoid character-conversion ambiguity.
  - Use `--legacy-win1252` only for legacy `CHARSET NONE` cases where default decoding is known to be wrong.
  - Use `--sanitize-text` and/or `--escape-newlines` when downstream parser/importer has issues with control chars or multiline literals.

- Operational behavior for large exports
  - Use `--split-size-mb` for very large outputs to avoid oversized single files.
  - Use `--progress-every` to monitor long-running exports.
  - Use `--continue-on-error` only when partial export is acceptable and row-level failures will be reviewed later.
  - `--commit-every` affects generated script transaction boundaries during import, not extraction consistency from source.

## Examples
```powershell
SkyFBTool export --database "C:\data\erp.fdb" --table CUSTOMERS --output "C:\exports\"
SkyFBTool export --database "C:\data\erp.fdb" --table ORDERS --filter "STATUS = 'A'" --output "C:\exports\orders.sql"
SkyFBTool export --database "C:\data\erp.fdb" --table ITEMS --query-file ".\sql\items.sql" --split-size-mb 200 --output "C:\exports\"
SkyFBTool export --database "C:\data\erp.fdb" --table CUSTOMERS --insert-mode upsert --escape-newlines --output "C:\exports\customers_upsert.sql"
```
