# `ddl-analyze` command

## What it does
Analyzes schema structural risk (PK, FK, indexes, duplication, unknown types) and generates:
- structured report (`.json`)
- HTML report (`.html`)

## How to use
```powershell
SkyFBTool ddl-analyze --input INPUT --output PREFIX [options]
```

## Accepted inputs
- `.schema.json`
- `.sql` (with or without side-by-side `.schema.json`)

## Main options
- `--ignore-table-prefix`: ignores tables by prefix (repeatable).
- `--ignore-table-prefixes`: comma-separated prefix list.
- `--severity-config`: JSON file to override severity by finding code.

## Examples
```powershell
SkyFBTool ddl-analyze --input "C:\ddl\source.schema.json" --output "C:\ddl\analysis"
SkyFBTool ddl-analyze --input "C:\ddl\source.sql" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$ --output "C:\ddl\analysis"
SkyFBTool ddl-analyze --input "C:\ddl\source.sql" --severity-config ".\examples\ddl-severity.sample.json" --output "C:\ddl\analysis_custom"
```

## Output example
```text
Starting DDL analysis...

Analysis finished.
Analysis JSON: C:\ddl\analysis.json
Report       : C:\ddl\analysis.html
```
