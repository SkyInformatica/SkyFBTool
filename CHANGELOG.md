# Changelog

English | [Português (Brasil)](./CHANGELOG.pt-BR.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `ddl-analyze` now reports procedures, functions, and triggers without a valid PSQL body.
- `ddl-extract` now preserves procedures, functions, and triggers with empty metadata source for downstream analysis, while emitting warning comments instead of invalid SQL for those objects.

### Changed
- `ddl-analyze` now has an internal rule engine structure, with table structure, foreign key, index, field compatibility, and PSQL body validations moved into dedicated rule classes.

### Fixed
- `ddl-extract` now filters Firebird function metadata more precisely so legacy UDFs, UDR functions, and package functions are not misclassified as top-level PSQL stored functions.

## [0.6.3] - 2026-07-04

### Security
- Updated `Scriban` from 7.1.0 to 7.2.5 to remove known denial-of-service vulnerability advisories affecting 7.1.0.

### Fixed
- `ddl-analyze` no longer reports expression indexes such as `UPPER(DESCRIPTION)` as `INDICE_COLUNA_INEXISTENTE`.

## [0.6.2] - 2026-05-13

### Added
- Integration coverage was expanded for DDL report flows:
  - batch `ddl-analyze` validates `none` highest severity for databases without findings;
  - `ddl-diff` validates HTML report print-style and visual KPI markers generation.
- New `create-db` command to provision Firebird database files with explicit operational options (`charset`, `page-size`, `forced-writes`) and safe overwrite behavior.
- `create-db` now supports `--ddl-file` to bootstrap schema immediately after database creation by executing an extracted SQL script.

### Changed
- PT-BR console message for `ddl-analyze --databases-batch` was refined to clearer wording (`Padrão de bancos correspondeu a ... arquivo(s)`).

### Fixed
- `create-db` now validates `--ddl-file` existence before attempting database creation/connection, ensuring deterministic `FileNotFoundException` behavior in CLI tests across environments.
- `ddl-extract`/`create-db --ddl-file` pipeline now handles critical schema bootstrap compatibility scenarios:
  - deterministic ordering for PK/UNIQUE/FK to avoid metadata dependency failures;
  - Firebird descending index syntax generation (`CREATE DESCENDING INDEX`);
  - procedure parameter default normalization for valid PSQL signatures;
  - dependency-aware routine ordering including `FROM`/`JOIN` usage patterns;
  - circular routine references handled through two-phase procedure emission (stub + full body);
  - Firebird modern type mapping correction (`DOUBLE PRECISION`, `TIME WITH TIME ZONE`, `TIMESTAMP WITH TIME ZONE`);
  - extraction and generation of custom `EXCEPTION` objects required by procedures.
- `create-db` now consistently propagates detected CLI locale to DDL import output, avoiding mixed PT-BR/EN console messages in a single run.

## [0.5.0] - 2026-05-10

### Added
- Import/export now apply automatic transient retry policy (up to 3 attempts) for command execution and file write instability scenarios.
- `ddl-diff` now supports `--include-domains` to optionally compare `DOMAIN` objects, while ignoring them by default for practical reviews.

### Changed
- `ddl-analyze --database` operational analysis was hardened with explicit status/error classification for MON$ collection outcomes (success, partial, or failure context in report metadata).
- `ddl-analyze` report layout and severity/priority visuals were refined for better consistency in rich and batch HTML reports.
- `ddl-analyze` HTML rendering now removes unused risk filter fields (`ScoreRisco`, `Prioridade`) from internal payload/output model.
- `export` and `import` now share standardized console progress behavior: live dynamic line in interactive terminals, periodic fixed checkpoints (50k units or 30s), and CI-safe fixed-line fallback for redirected output.
- `ddl-diff` now emits SQL in deterministic dependency-aware order (drop constraints, create/alter structures, PK, indexes, then FK) to reduce apply-time dependency failures.
- `ddl-diff` now ignores `DOMAIN` differences by default and only includes them when `--include-domains` is explicitly enabled.
- Command docs (EN/PT-BR) and README were updated to reflect dependency ordering and transient retry behavior.
- DDL sample artifacts and command docs were refreshed to reflect current report/UI behavior.
- README (EN/PT-BR) was restructured as a strategic documentation portal, and conceptual docs were organized under `docs/concepts/en` and `docs/concepts/pt-BR` with bilingual navigation.
- Batch DDL analysis summary now uses `Not applicable` for databases without findings, preventing false urgency in highest-severity presentation.

### Fixed
- Batch `import` summary now correctly classifies files with SQL command errors under `--continue-on-error` as `Succeeded with errors` instead of plain `Succeeded`.
- DDL report titles now preserve UTF-8 accents correctly in generated PDF/print flows (for example: `Análise de Risco DDL`).
- Silent exception handling in maintenance timestamp collection was replaced with explicit resilient handling to keep diagnostics consistent.


## [0.4.0] - 2026-04-29

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
- Release pipeline/versioning was aligned to derive build version from Git tag (`v*`).
- `ddl-analyze` docs and sample reports were refreshed.

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

[Unreleased]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.6.2...HEAD
[0.6.2]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.5.0...v0.6.2
[0.5.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
