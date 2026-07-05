# Assisted Programming Guide (SkyFBTool)

Guide for AI assistants working in this repository. Keep this file compact so routine tasks spend fewer tokens on repeated rules.

## 1) Required Reading and Precedence
- Before any change, read `DOCS_STANDARD.md` when it exists.
- Apply `DOCS_STANDARD.md` together with this file.
- If they conflict, `DOCS_STANDARD.md` controls documentation/language rules and this file controls implementation rules.

## 2) Project Context
- .NET 8 CLI tool for Firebird SQL export/import.
- Entry point: `SkyFBTool/Program.cs`.
- Main areas:
  - Export: `SkyFBTool/Services/Export/`
  - Import: `SkyFBTool/Services/Import/`
  - DDL/schema analysis: `SkyFBTool/Services/Ddl/`
  - Shared infrastructure: `SkyFBTool/Infra/`
  - Contracts/options: `SkyFBTool/Core/`

## 3) Non-Negotiable Constraints
- Preserve streaming behavior: do not load full SQL files into memory and do not materialize full tables during export.
- Keep compatibility with Firebird 2.5, 3.0, 4.0, and 5.0.
- Preserve charset behavior: `SET NAMES` on export, `SET NAMES` detection on import, and `--force-win1252` support.
- Avoid broad refactors unless directly needed by the task.
- Do not add dependencies unless they solve a real problem; document any new dependency in the README.
- Do not change existing flag behavior unless necessary.

## 4) Responsibility Map
- `Program.cs`: manual argument parsing and command routing.
- `ExportadorTabelaFirebird`: export SQL header, SELECT execution, INSERT generation, commits, progress, and export error logging.
- `ConstrutorInsert`: SQL literal serialization, including text and BLOBs.
- `ImportadorSql`: streaming SQL parser, comments/strings, `SET TERM`, transactions, metrics, and index disable/reenable flow.
- `ExecutorSql`: individual SQL command execution, including `COMMIT` and `SET`.
- `SkyFBTool/Services/Ddl/Rules/`: structural DDL analysis rules.

## 5) Implementation Rules
- Prefer small, localized changes that follow existing Portuguese code names and project conventions.
- Reuse existing helpers before adding new logic, especially for charset handling, SQL serialization, command parsing, and error handling.
- Keep responsibilities separate: parsing, validation, execution, logging, report generation, and user output should not be mixed unnecessarily.
- Keep methods readable; extract helpers when logic becomes long, deeply nested, or duplicated.
- Avoid comments unless they clarify non-obvious logic.
- When SQL concatenation is unavoidable, validate identifiers/inputs and keep useful context in errors.
- For large logs, append to dedicated files and keep console messages short.

## 6) CLI, Docs, and Localization
- Runtime/report output defaults to English.
- When `IdiomaSaidaDetector` identifies `pt-BR`, user-facing messages must have a natural Portuguese variant with correct accents.
- Use shared localization helpers for new localized runtime text; do not concatenate raw bilingual messages.
- New options must have a sensible default and be reflected in parser, help, README, and command docs when behavior changes.
- Keep `README.md`/`CHANGELOG.md` in English and `README.pt-BR.md`/`CHANGELOG.pt-BR.md` synchronized when public behavior changes.

## 7) Known Risks
- `--where` is free-form Firebird SQL; existing dangerous-token validation is not full SQL validation.
- Large import/export files need attention to disk space, log size, and streaming guarantees.
- New CLI parameters must keep parser, help, README, and command documentation aligned.

## 8) Validation Policy
- Minimum before finishing code changes: local build plus focused tests for the affected flow.
- For import/export behavior changes, run at least one representative affected flow when practical.
- For shared infrastructure, DDL rule engine, parser, or release/commit preparation, run broader or full tests as appropriate.
- Integration tests are required only when the task affects database-backed behavior or the user asks for them.
- If validation cannot be run, state exactly what was not run and why.

## 9) Git Policy
- Never revert user changes unless explicitly requested.
- Commit messages must be in English, imperative, and specific, for example: `Add DDL severity override config`.
- Avoid vague commit messages such as `fixes`, `update`, or `adjustments`.
- Keep multiple commits scoped and consistent.

## 10) Final Checklist
Before concluding, verify:
- The change addresses exactly the request.
- Streaming, charset handling, and Firebird compatibility were preserved.
- Existing helpers/patterns were reused and no unnecessary duplication was introduced.
- Error handling includes enough context for diagnosis.
- CLI help, README, command docs, and changelog were updated when behavior changed.
- New user-facing text follows English default plus localized PT-BR when applicable.
- Tests/build appropriate to the risk were run.

## 11) Token Economy
- Prefer targeted file reads and `rg` searches over dumping large files.
- Summarize command output unless the user asks for full output.
- Run focused tests first; expand to full/integration tests when risk or user request justifies it.
- Keep progress updates short and tied to meaningful steps.
