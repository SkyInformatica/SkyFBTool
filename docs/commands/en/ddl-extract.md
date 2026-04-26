# `ddl-extract` command

## What it does
Extracts Firebird schema metadata and generates:
- readable DDL (`.sql`)
- normalized snapshot (`.schema.json`)

## How to use
```powershell
SkyFBTool ddl-extract --database PATH.fdb --output PREFIX [options]
```

## All options
- `--database`: source database.
- `--output`: output prefix, file base, or directory.
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--charset`: optional connection charset.

## Examples
```powershell
SkyFBTool ddl-extract --database "C:\data\source.fdb" --output "C:\ddl\source"
SkyFBTool ddl-extract --database "C:\data\target.fdb" --output "C:\ddl\target"
```
