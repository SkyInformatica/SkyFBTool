# `ddl-analyze` command

## What it does
Analyzes schema structural risk (PK, FK, indexes, duplication, unknown types) and generates:
- structured report (`.json`)
- HTML report (`.html`)
- in batch mode, a DBA-oriented consolidated summary (`batch_analysis_summary_*.json` and `.html`)

## How to use
```powershell
SkyFBTool ddl-analyze --input INPUT --output PREFIX [options]
SkyFBTool ddl-analyze --database PATH.fdb --output PREFIX [options]
SkyFBTool ddl-analyze --databases-batch "C:\data\*.fdb" --output DIRECTORY [options]
```

## Accepted inputs
- `.schema.json`
- `.sql` (with or without side-by-side `.schema.json`)
- direct database connection (`--database`)
- batch database wildcard (`--databases-batch`)

## Main options
- `--database`, `--host`, `--port`, `--user`, `--password`, `--charset`: direct DB source.
- `--databases-batch`: wildcard pattern (`*`, `?`) for batch analysis.
- `--ignore-table-prefix`: ignores tables by prefix (repeatable).
- `--ignore-table-prefixes`: comma-separated prefix list.
- `--severity-config`: JSON file to override severity by finding code.

## Examples
```powershell
SkyFBTool ddl-analyze --input "C:\ddl\source.schema.json" --output "C:\ddl\analysis"
SkyFBTool ddl-analyze --input "C:\ddl\source.sql" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$ --output "C:\ddl\analysis"
SkyFBTool ddl-analyze --database "C:\data\source.fdb" --output "C:\ddl\analysis_from_db"
SkyFBTool ddl-analyze --databases-batch "C:\data\*.fdb" --output "C:\ddl\analysis_batch\"
SkyFBTool ddl-analyze --input "C:\ddl\source.sql" --severity-config ".\docs\examples\ddl-severity.sample.json" --output "C:\ddl\analysis_custom"
```

## Output example
```text
Starting DDL analysis...

Batch analysis finished.
Batch summary JSON  : C:\ddl\analysis_batch\batch_analysis_summary_20260426_103000_123.json
Batch summary report: C:\ddl\analysis_batch\batch_analysis_summary_20260426_103000_123.html
```
