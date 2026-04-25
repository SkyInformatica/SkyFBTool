# `ddl-extract` command

## What it does
Extracts Firebird schema metadata and generates:
- readable DDL (`.sql`)
- normalized snapshot (`.schema.json`)

## How to use
```powershell
SkyFBTool ddl-extract --database PATH.fdb --output PREFIX [options]
```

## Main options
- `--database`: source database.
- `--output`: output prefix, base file path, or directory.
- `--charset`: optional connection charset.

## Examples
```powershell
SkyFBTool ddl-extract --database "C:\data\source.fdb" --output "C:\ddl\source"
SkyFBTool ddl-extract --database "C:\data\target.fdb" --output "C:\ddl\target"
```

## Output example
```text
Starting DDL extraction...

Extraction finished.
DDL SQL    : C:\ddl\source.sql
Schema JSON: C:\ddl\source.schema.json
```
