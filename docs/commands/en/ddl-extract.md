# `ddl-extract` command

## What it does
Extracts Firebird schema metadata and generates two synchronized outputs:
- readable DDL script (`.sql`) for human inspection
- normalized schema snapshot (`.schema.json`) for machine diff/analyze workflows

`ddl-extract` is the canonical entry point before `ddl-diff` and `ddl-analyze` when you want reproducible metadata artifacts.

## When to use
- DBA: capture current schema baseline before maintenance or migration.
- Developer: produce versionable schema snapshots for review and CI comparisons.

## How to use
```powershell
SkyFBTool ddl-extract --database PATH.fdb --output PREFIX [options]
```

## All options
- `--database`: source Firebird database.
- `--output`: output prefix/file base/directory.
  - Prefix/file base: generates `<prefix>.sql` and `<prefix>.schema.json`.
  - Directory: tool generates timestamped base name inside the directory.
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--charset`: optional connection charset; use when DB metadata/text requires explicit charset handling.

## Rules and operational guidance
- Use stable credentials/connection settings between source and target extractions if the goal is deterministic `ddl-diff`.
- Keep extracted `.schema.json` under version control when tracking schema evolution over time.
- Prefer extracting from a consistent database state (outside active migration windows) to avoid transient metadata diffs.
- Use explicit output naming conventions (for example, environment/date) for auditability.

## Extraction failure categories
When extraction fails, CLI classifies root cause:
- `incompatible_ods`
- `permission_denied`
- `database_file_access`
- `metadata_query_failure`
- `connection_failure`
- `unknown`

## Practical workflow
1. Run `ddl-extract` for source DB.
2. Run `ddl-extract` for target DB.
3. Use both `.schema.json` files in `ddl-diff`.
4. Optionally run `ddl-analyze` over either snapshot or direct DB mode.

## Examples
```powershell
SkyFBTool ddl-extract --database "C:\data\source.fdb" --output "C:\ddl\source"
SkyFBTool ddl-extract --database "C:\data\target.fdb" --output "C:\ddl\target"
SkyFBTool ddl-extract --database "C:\data\prod.fdb" --charset WIN1252 --output "C:\ddl\prod_2026_05_01"
```
