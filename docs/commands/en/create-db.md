[Português (Brasil)](../pt-BR/create-db.md)

# `create-db`

Creates a new Firebird database file.

## Purpose

Use `create-db` to provision a database file with explicit operational parameters (charset, page size, forced writes), with safe behavior by default.

## Options

- `--database <path>`: target `.fdb` file path (required)
- `--host <server>`: Firebird host (default: `localhost`)
- `--port <number>`: Firebird port (default: `3050`)
- `--user <name>`: user (default: `sysdba`)
- `--password <value>`: password (default: `masterkey`)
- `--charset <name>`: database charset (default: `UTF8`)
- `--page-size <number>`: page size in bytes (default: `8192`)
- `--forced-writes on|off`: forced writes mode (default: `on`)
- `--overwrite`: recreate file if it already exists
- `--ddl-file <path.sql>`: applies SQL script right after database creation

## Behavior and safety

- If the target file already exists, command fails by default.
- `--overwrite` is required to recreate an existing file.
- The target directory is created automatically when needed.
- If `--ddl-file` is provided, script execution runs in fail-fast mode (`continue-on-error` disabled).

## Example

```powershell
SkyFBTool create-db --database "C:\data\new_db.fdb" --charset UTF8 --page-size 8192
SkyFBTool create-db --database "C:\data\new_db.fdb" --ddl-file "C:\ddl\extracted_schema.sql"
```
