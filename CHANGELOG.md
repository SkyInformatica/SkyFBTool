# Changelog

English | [Português (Brasil)](./CHANGELOG.pt-BR.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `ddl-analyze` now supports `--volume-analysis on|off` (default: `on`) to explicitly enable/disable SQL-based volume-priority analysis.
- `ddl-analyze` now supports `--volume-count-exact on|off` (default: `off`) to optionally run exact `COUNT(*)` per table for volume analysis.
- `ddl-analyze --database` report metadata now includes estimated last maintenance timestamp from `MON$DATABASE.MON$CREATION_DATE` (database creation/last restore).

### Changed
- `FK_SEM_INDICE_COBERTURA` findings now include richer context in report text (child table/columns and parent table/columns).
- `INDICE_DUPLICADO` findings now include the computed index signature to make duplicate validation easier for DBAs.
- `ddl-analyze` now emits volume-priority operational findings (`OPERACIONAL_VOLUME_PRIORIDADE_ALTA|MEDIA|BAIXA`) using lightweight index-based estimates in DB mode.
- `ddl-analyze` HTML report now includes a table-focused remediation prioritization section (`Tables prioritized for remediation`) with `Priority` (`P0..P3`), `Risk index`, and `Count`.
- `ddl-analyze` report layout now shows the priority legend (`P0..P3`) next to severity criteria and aligns summary panels with fixed-height scroll areas for long outputs.

### Fixed
- False positives for `FK_SEM_INDICE_COBERTURA` were fixed by considering FK support index metadata (constraint-bound index) in both DB extraction and SQL snapshot analysis.

## [0.3.0] - 2026-04-28

### Added
- `ddl-analyze` now supports direct database input (`--database` + connection options), extracting metadata internally before analysis.
- `ddl-analyze` now supports batch mode via `--databases-batch` (`*`, `?`) to run analysis over multiple `.fdb` files.
- `ddl-analyze --database` now includes operational findings from Firebird monitoring tables (`MON$`) in the same risk report.
- `import` now supports batch input mode to execute multiple SQL files using wildcard patterns (`--inputs-batch`, aliases: `--input-batch`, `--scripts-batch`).
- Import now always generates a per-execution log file with unique name (`*_import_log_*.log`), including explicit success/error completion status.
- `ddl-extract` now classifies extraction failures by root category (`incompatible_ods`, `permission_denied`, `database_file_access`, `metadata_query_failure`, `connection_failure`, `unknown`).
- `ddl-analyze` now detects redundant prefix indexes (`REDUNDANT_PREFIX_INDEX`) as optimization findings.
- New reproducible `ddl-analyze` sample files were added under `docs/examples` (`sample` and `sample-rich` in `.sql/.json/.html`).
- `ddl-analyze` now supports `--description` to include contextual text in report metadata (`Description`) for both JSON and HTML outputs.

### Changed
- `ddl-analyze` help and command docs were updated to document file mode and direct DB mode.
- Command documentation was completed to explicitly list all supported CLI parameters and aliases (EN/PT-BR).
- `ddl-analyze` report documentation now links directly to live HTML examples instead of static screenshots.
- `ddl-analyze` HTML report print styles were improved for better PDF export (A4 layout, better table wrapping, cleaner page breaks).
- Removed obsolete PNG screenshot assets from `docs/examples` and cleaned stale solution mappings.

### Fixed
- `import` default behavior for `--continue-on-error` was corrected: without the flag, import now stops on first SQL execution error.

## [0.2.0] - 2026-04-25

### Added
- Console warning for `--filter-file` and `--query-file` when file size exceeds 64 KB.
- More resilient CLI argument handling for PowerShell cases with `--output` ending in a trailing backslash.
- New `ddl-extract` command to export normalized schema (`.sql` + `.schema.json`).
- New `ddl-diff` command to compare source/target schemas and generate `.sql`, `.json`, and `.md` reports.
- `ddl-diff` now also generates an `.html` visual report.
- `ddl-analyze` now accepts `.sql` directly (with internal DDL parser fallback when `.schema.json` is not present).
- New severity override support for `ddl-analyze` via `--severity-config` (`overrides`, `code`, `severity`).
- New command documentation under `docs/commands/en` and `docs/commands/pt-BR`.

### Changed
- Export summary output is now aligned and easier to read.
- Missing `--table` error now includes guidance for trailing backslash usage in PowerShell.
- CLI was reorganized by context (`Cli/Commands` and `Cli/Common`) with a minimal `Program.cs` entrypoint.
- DDL report and DDL command output now use OS culture detection (`en` default, `pt-BR` localized).
- `ddl-diff` HTML generation was extracted to a dedicated renderer/template (Scriban-based).
- FK validation in DDL analysis was split into smaller functions by validation type.
- Unknown CLI option validation was standardized across command handlers.
- `ddl-analyze` report now includes explicit severity criteria in HTML.
- Severity config examples were standardized to English aliases in `docs/examples/ddl-severity.sample.json`.

### Fixed
- Portuguese report messages for DDL findings (`description`/`recommendation`) were normalized with correct accentuation.

## [0.1.0] - 2026-04-21

### Added
- Main commands: `export` and `import`.
- Export support for `--filter`, `--filter-file`, `--query-file`, and `--target-table`.
- Automatic output splitting with `--split-size-mb`.
- Firebird 2.5, 3.0, 4.0, and 5.0 compatibility for export/import workflows.
- Unit and integration test suite.

[Unreleased]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
