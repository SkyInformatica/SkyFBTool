# Changelog

English | [Português (Brasil)](./CHANGELOG.pt-BR.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Console warning for `--filter-file` and `--query-file` when file size exceeds 64 KB.
- More resilient CLI argument handling for PowerShell cases with `--output` ending in a trailing backslash.

### Changed
- Export summary output is now aligned and easier to read.
- Missing `--table` error now includes guidance for trailing backslash usage in PowerShell.

## [0.1.0] - 2026-04-21

### Added
- Main commands: `export` and `import`.
- Export support for `--filter`, `--filter-file`, `--query-file`, and `--target-table`.
- Automatic output splitting with `--split-size-mb`.
- Firebird 2.5, 3.0, 4.0, and 5.0 compatibility for export/import workflows.
- Unit and integration test suite.

[Unreleased]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
