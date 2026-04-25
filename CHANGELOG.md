# Changelog

English | [Português (Brasil)](./CHANGELOG.pt-BR.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
