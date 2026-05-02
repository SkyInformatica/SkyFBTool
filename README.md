# SkyFBTool

English | [Português (Brasil)](./README.pt-BR.md)

SkyFBTool is a .NET 8 CLI for Firebird data export/import (2.5 / 3.0 / 4.0 / 5.0), focused on large datasets, streaming execution, and charset-safe workflows.

## Target audience

- DBA: operational execution, schema comparison, risk triage, and controlled rollout checks.
- Developer: reproducible schema artifacts, migration review, CI-friendly outputs, and automated validation flows.

## Command selection guide

- Need to move table data to SQL script: use `export`.
- Need to execute SQL script(s) on a database: use `import` (or `exec-sql` for maintenance intent).
- Need schema snapshots (`.sql` + `.schema.json`): use `ddl-extract`.
- Need structural comparison between two schemas: use `ddl-diff`.
- Need risk/prioritization report with severities and operational signals: use `ddl-analyze`.

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

## Recommended workflows

### 1) Data migration workflow (DBA/operations)
1. `export` from source table/query.
2. Review generated SQL and file split/charset settings.
3. `import` into target with progress and log monitoring.
4. Validate import log and final summary.

### 2) Schema promotion workflow (DBA + dev)
1. `ddl-extract` from source and target environments.
2. `ddl-diff` to generate SQL/json/html comparison.
3. Review `ddl-diff` HTML and SQL in staging.
4. Apply approved SQL and re-run `ddl-diff` to confirm convergence.

### 3) Risk triage workflow (DBA)
1. Run `ddl-analyze` (prefer `--database` mode when possible).
2. Start from table prioritization section in HTML report.
3. Resolve `critical/high` findings first, then `medium`.
4. Keep `low` findings as optimization backlog after plan validation.

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
SkyFBTool ddl-analyze --input "C:\ddl\source.schema.json" --description "analysis on XYZ database" --output "C:\ddl\analysis_with_context"
```

Notes:
- `ddl-extract` classifies extraction failures by root category (`incompatible_ods`, `permission_denied`, `database_file_access`, `metadata_query_failure`, `connection_failure`, `unknown`).
- `ddl-diff` output files: `.sql`, `.json`, and `.html`.
- `ddl-diff` report includes Top 10 critical target findings (with severity), suggested SQL block order, and a post-apply checklist.
- `ddl-analyze` output files: `.json` and `.html`, with summary by code/table and HTML filters.
- `ddl-analyze` HTML report includes a **Tables prioritized for remediation** section with `Priority` (`P0..P3`), `Risk index`, and `Count`, plus a priority legend next to severity criteria.
- In `ddl-analyze --databases-batch`, an additional consolidated summary is generated: `batch_analysis_summary_*.json` and `.html`.
- `ddl-analyze` supports two input modes: file (`--input/--source`) or direct DB connection (`--database` + connection options).
- In `ddl-analyze --database`, the report also includes operational findings based on Firebird monitoring tables (`MON$`), such as transaction retention pressure signals.
- `ddl-analyze` detects redundant prefix indexes (for example, `(A)` vs `(A,B)`) as optimization findings.
- `ddl-analyze` supports batch DB mode with `--databases-batch` (`*` and `?`) to analyze multiple `.fdb` files.
- `ddl-analyze` accepts `--ignore-table-prefix` (repeatable) and `--ignore-table-prefixes` (comma-separated list) to suppress technical-table noise.
- `ddl-analyze` accepts `--severity-config` to override severity by finding code.
- `ddl-analyze` accepts `--description` to include contextual text in JSON and HTML report metadata.
- `ddl-analyze` supports `--volume-analysis on|off` (default `on`) to enable/disable volume-priority analysis.
- `ddl-analyze` uses index-based volume estimation by default and executes exact `COUNT(*)` only when `--volume-count-exact on` is explicitly set.
- DDL reports and CLI runtime messages follow OS culture detection (`English` by default, `pt-BR` when the system culture is Brazilian Portuguese).
- Use `docs/examples/ddl-severity.sample.json` as the reference schema (it covers all current finding codes).
- Reproducible `ddl-analyze` sample outputs: `docs/examples/ddl-analyze-sample*.{sql,json,html}`.
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
- batch summary statuses:
  - `Succeeded`: file completed without SQL command errors
  - `Succeeded with errors`: file completed with command errors under `--continue-on-error`
  - `Failed`: file aborted by fatal error

## Tests

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

### What Tests Guarantee Today

- Export:
  - safe/valid `SELECT` composition (`table`, `columns`, `filter`, `query-file`);
  - SQL generation consistency (`INSERT`/`UPSERT`, BLOB formats, newline escaping, `commit-every`);
  - charset and legacy behavior coverage (`UTF8`, `WIN1252`, `ISO8859_1`, `NONE` + legacy mode);
  - computed/read-only column exclusion and export/import round-trip scenarios.
- Import / SQL execution:
  - streaming parser behavior (`SET TERM`, comments, string literals);
  - fail-fast vs `--continue-on-error` behavior and execution logging;
  - batch input flow, parameter validation, and core progress/commit behavior.
- DDL workflows:
  - `ddl-extract` snapshot/DDL generation for core objects;
  - `ddl-diff` structural change detection and SQL suggestion behavior;
  - `ddl-analyze` structural validations, severity override, and summary composition;
  - operational checks (`MON$`) core thresholds and batch summary aggregation.
- Infra and CLI:
  - charset detection/resolution utilities, output file split behavior;
  - CLI option validation and contextual error classification.

### Coverage Gaps and Next Priorities

- `MON$` operational resilience across Firebird versions/permissions edge cases.
- `ddl-diff` deeper real-world dependency combinations beyond current ordering baseline.
- Import/export long-run stress validation in production-like datasets (resource pressure, long-duration stability).
- Batch mixed-result flows (partial failures, highly heterogeneous databases).

Integration tests:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Troubleshooting quick guide

- Script failed with SQL errors:
  - Check per-run log file (`*_import_log_*.log`) and final summary.
- Charset/accent issues:
  - Set explicit `--charset`; use `--legacy-win1252` only for confirmed legacy `CHARSET NONE` cases.
- Very large execution/log output:
  - Use split/progress options and prefer redirected output in CI pipelines.
- `ddl-analyze` operational findings missing:
  - Confirm DB mode (`--database` or `--databases-batch`) and MON$ access permissions.

## Documentation Standard

- [DOCS_STANDARD.md](./DOCS_STANDARD.md)

## Command Documentation

- `export`: [docs/commands/en/export.md](./docs/commands/en/export.md)
- `import`: [docs/commands/en/import.md](./docs/commands/en/import.md)
- `exec-sql`: [docs/commands/en/exec-sql.md](./docs/commands/en/exec-sql.md)
- `ddl-extract`: [docs/commands/en/ddl-extract.md](./docs/commands/en/ddl-extract.md)
- `ddl-diff`: [docs/commands/en/ddl-diff.md](./docs/commands/en/ddl-diff.md)
- `ddl-analyze`: [docs/commands/en/ddl-analyze.md](./docs/commands/en/ddl-analyze.md)
- `ddl-analyze` severity/validation criteria: [docs/commands/en/ddl-analyze-severity-and-validations.md](./docs/commands/en/ddl-analyze-severity-and-validations.md)

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
