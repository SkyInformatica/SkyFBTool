# SkyFBTool

English | [Português (Brasil)](./README.pt-BR.md)

SkyFBTool is a .NET 8 CLI for Firebird data export/import (2.5 / 3.0 / 4.0 / 5.0), focused on large datasets, streaming execution, and charset-safe workflows.

## What's New

- [CHANGELOG.md](./CHANGELOG.md)
- [Releases](https://github.com/SkyInformatica/SkyFBTool/releases)

## Automated Releases

This repository includes a GitHub Actions pipeline at `.github/workflows/release.yml`.

How it works:
- Trigger: push a tag in format `v*` (example: `v0.1.0`)
- Pipeline: restore, build, test, publish (`win-x64` and `linux-x64`)
- Output: GitHub Release with compiled artifacts (`.tar.gz`)

Tag command example:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Main Features

- `export`, `import`, and `exec-sql` commands
- `ddl-extract`, `ddl-diff`, and `ddl-analyze` commands for extraction, schema comparison, and DDL risk analysis
- Streaming export/import for large SQL files
- `--filter`, `--filter-file`, and advanced `--query-file`
- Target table remap with `--target-table`
- `--blob-format` (`Hex` or `Base64`)
- `--insert-mode` (`insert` or `upsert` with `UPDATE OR INSERT ... MATCHING`)
- Configurable `--commit-every` and `--progress-every`
- File splitting with `--split-size-mb` (default: 100 MB)
- Legacy charset mode for `CHARSET NONE` via `--legacy-win1252`
- Warning for large filter/query files (> 64 KB)

## Code Organization

- `Program.cs`: minimal entrypoint
- `Cli/CliApp.cs`: CLI routing + help
- `Cli/Commands/*`: one file per command (`export`, `import`, `ddl-extract`, `ddl-diff`, `ddl-analyze`)
- `Cli/Common/*`: shared argument parsing helpers
- `Services/*`: context-specific logic (Export, Import, Ddl)
- `Infra/*`: technical adapters (connection, encoding, files)

## Usage

```powershell
SkyFBTool export [options]
SkyFBTool import [options]
SkyFBTool exec-sql [options]
SkyFBTool ddl-extract [options]
SkyFBTool ddl-diff [options]
SkyFBTool ddl-analyze [options]
```

### Export example

```powershell
SkyFBTool export --database "C:\data\sample.fdb" --table "SAMPLE_TABLE" --output "C:\exports\" --commit-every 10000
```

### Import example

```powershell
SkyFBTool import --database "C:\data\sample.fdb" --input "C:\exports\sample_table.sql" --continue-on-error
SkyFBTool import --database "C:\data\sample.fdb" --inputs-batch "C:\exports\*.sql" --continue-on-error
SkyFBTool exec-sql --database "C:\data\sample.fdb" --script "C:\scripts\patch.sql" --continue-on-error
```

### DDL extract and diff examples

```powershell
SkyFBTool ddl-extract --database "C:\data\source.fdb" --output "C:\ddl\source"
SkyFBTool ddl-extract --database "C:\data\target.fdb" --output "C:\ddl\target"
SkyFBTool ddl-diff --source "C:\ddl\source.schema.json" --target "C:\ddl\target.schema.json" --output "C:\ddl\diff"
SkyFBTool ddl-analyze --input "C:\ddl\source.schema.json" --output "C:\ddl\analysis"
SkyFBTool ddl-analyze --input "C:\ddl\source.schema.json" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$
SkyFBTool ddl-analyze --database "C:\data\source.fdb" --output "C:\ddl\analysis_from_db"
SkyFBTool ddl-analyze --databases-batch "C:\data\*.fdb" --output "C:\ddl\analysis_batch\"
SkyFBTool ddl-analyze --input "C:\ddl\source.schema.json" --severity-config ".\docs\examples\ddl-severity.sample.json"
```

Notes:
- DDL report/output language uses OS culture detection (`English` by default, `pt-BR` localized).
- `ddl-extract` classifies extraction failures by root category (`incompatible_ods`, `permission_denied`, `database_file_access`, `metadata_query_failure`, `connection_failure`, `unknown`).
- `ddl-diff` output files: `.sql`, `.json`, and `.html`.
- `ddl-diff` report includes Top 10 critical target findings (with severity), suggested SQL block order, and a post-apply checklist.
- `ddl-analyze` output files: `.json` and `.html`, with summary by code/table and HTML filters.
- In `ddl-analyze --databases-batch`, an additional consolidated summary is generated: `batch_analysis_summary_*.json` and `.html`.
- `ddl-analyze` supports two input modes: file (`--input/--source`) or direct DB connection (`--database` + connection options).
- In `ddl-analyze --database`, the report also includes operational findings based on Firebird monitoring tables (`MON$`), such as transaction retention pressure signals.
- `ddl-analyze` supports batch DB mode with `--databases-batch` (`*` and `?`) to analyze multiple `.fdb` files.
- `ddl-analyze` accepts `--ignore-table-prefix` (repeatable) and `--ignore-table-prefixes` (comma-separated list) to suppress technical-table noise.
- `ddl-analyze` accepts `--severity-config` to override severity by finding code.
- Use `docs/examples/ddl-severity.sample.json` as the reference schema (it covers all current finding codes).
- DDL analyze report screenshot example: `docs/examples/ddl-analyze-report-example.png`.
- DDL analyze batch summary screenshot example: `docs/examples/ddl-analyze-batch-summary-example.png`.
- Accepted severity values: `critical`, `high`, `medium`, `low`.
- JSON schema is English-only: `overrides`, `code`, `severity`.

## Key Export Options

- `--database` Firebird database path
- `--table` source table
- `--target-table` target table in generated `INSERT`s
- `--output` output file or directory
- `--host` Firebird host (default: `localhost`)
- `--port` Firebird port (default: `3050`)
- `--user` Firebird user (default: `sysdba`)
- `--password` Firebird password (default: `masterkey`)
- `--charset` `WIN1252 | ISO8859_1 | UTF8 | NONE`
- `--filter` simple condition (optional)
- `--filter-file` read simple condition from file
- `--query-file` read full `SELECT` from file (advanced mode)
- `--blob-format` `Hex | Base64`
- `--insert-mode` `insert | upsert` (`upsert` requires PK and writes `MATCHING`)
- `--commit-every` add `COMMIT` every N rows
- `--progress-every` progress interval
- `--split-size-mb` output split size in MB (`0` disables)
- `--legacy-win1252` legacy mode for `CHARSET NONE`
- `--sanitize-text` sanitize text values before writing SQL
- `--escape-newlines` escape line breaks in text fields
- `--continue-on-error` keep exporting even if one row fails

Rules:
- Do not combine `--query-file` with `--filter` or `--filter-file`.
- `--query-file` must contain a full `SELECT`.
- `--filter` accepts optional `WHERE` prefix (automatically removed).

## Key Import Options

- `--database` Firebird database path
- `--input` SQL file to import
- `--script` explicit alias for `--input`
- `--inputs-batch` wildcard pattern for multiple SQL input files
- `--input-batch` alias for `--inputs-batch`
- `--scripts-batch` alias for `--inputs-batch`
- `--host` Firebird host (default: `localhost`)
- `--port` Firebird port (default: `3050`)
- `--user` Firebird user (default: `sysdba`)
- `--password` Firebird password (default: `masterkey`)
- `--progress-every` progress interval
- `--continue-on-error` keep importing after command errors
- use only one input mode per run: `--input/--script` or `--inputs-batch`

## Tests

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

Integration tests:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Documentation Standard

- [DOCS_STANDARD.md](./DOCS_STANDARD.md)

## Command Documentation

- `export`: [docs/commands/en/export.md](./docs/commands/en/export.md)
- `import`: [docs/commands/en/import.md](./docs/commands/en/import.md)
- `exec-sql`: [docs/commands/en/exec-sql.md](./docs/commands/en/exec-sql.md)
- `ddl-extract`: [docs/commands/en/ddl-extract.md](./docs/commands/en/ddl-extract.md)
- `ddl-diff`: [docs/commands/en/ddl-diff.md](./docs/commands/en/ddl-diff.md)
- `ddl-analyze`: [docs/commands/en/ddl-analyze.md](./docs/commands/en/ddl-analyze.md)

## Dependencies

- `Scriban` is used to render `ddl-analyze` HTML reports from templates.

## Disclaimer

SkyFBTool is provided under the MIT license, "AS IS", without warranties of any kind.

The authors are not liable for:
- data loss
- database corruption
- execution failures
- direct or indirect damages
- misuse
- third-party impacts

Always validate in a staging/homologation environment before production use.

## License

MIT
