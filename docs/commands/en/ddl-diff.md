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

## Accepted inputs
- `.schema.json`
- `.sql` (if side-by-side `.schema.json` exists, it is preferred)
- raw `.sql` (internal parser)

## Examples
```powershell
SkyFBTool ddl-diff --source "C:\ddl\source.schema.json" --target "C:\ddl\target.schema.json" --output "C:\ddl\diff"
SkyFBTool ddl-diff --source "C:\ddl\source.sql" --target "C:\ddl\target.sql" --output "C:\ddl\diff_from_sql"
```

## Output example
```text
Starting DDL comparison...

Comparison finished.
Diff SQL   : C:\ddl\diff.sql
Diff JSON  : C:\ddl\diff.json
Report     : C:\ddl\diff.html
```
