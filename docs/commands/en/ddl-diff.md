# `ddl-diff` command

## What it does
Compares two schemas and generates:
- adjustment SQL (`.sql`)
- structured diff (`.json`)
- visual report (`.html`)

## How to use
```powershell
SkyFBTool ddl-diff --source SOURCE --target TARGET --output PREFIX
```

## All options
- `--source`: source input (`.schema.json` or `.sql`).
- `--source-ddl`: alias for `--source`.
- `--target`: target input (`.schema.json` or `.sql`).
- `--target-ddl`: alias for `--target`.
- `--output`: output prefix/file base/directory.

## Examples
```powershell
SkyFBTool ddl-diff --source "C:\ddl\source.schema.json" --target "C:\ddl\target.schema.json" --output "C:\ddl\diff"
SkyFBTool ddl-diff --source-ddl "C:\ddl\source.sql" --target-ddl "C:\ddl\target.sql" --output "C:\ddl\diff_from_sql"
```
