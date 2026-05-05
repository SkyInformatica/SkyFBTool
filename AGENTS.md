# Assisted Programming Guide (SkyFBTool)

This file guides AI assistants to make safe and consistent changes in the project.

## 0) Required Reading
- Before starting any change, read `DOCS_STANDARD.md` when it exists in the repository.
- Apply this `AGENTS.md` together with `DOCS_STANDARD.md`.
- If the two documents conflict, `DOCS_STANDARD.md` takes precedence for documentation and language rules, and this file takes precedence for project implementation rules.

## 1) Project Context
- Type: .NET 8 CLI tool for Firebird SQL export/import.
- Main entry point: `SkyFBTool/Program.cs`.
- Core areas:
    - Export: `SkyFBTool/Services/Export/`
    - Import: `SkyFBTool/Services/Import/`
    - Shared infrastructure: `SkyFBTool/Infra/`
    - Contracts/options: `SkyFBTool/Core/`

## 2) Mandatory Change Principles
- Preserve streaming processing:
    - do not load the full SQL file into memory;
    - do not materialize full tables for export.
- Keep compatibility with Firebird 2.5/3.0/4.0/5.0.
- Preserve charset behavior:
    - `SET NAMES` on export;
    - `SET NAMES` detection on import;
    - support for `--force-win1252` for legacy databases.
- Avoid broad refactoring unless it is directly needed by the task.
- When behavior changes, update the CLI help and README.

## 3) Quick Responsibility Map
- `Program.cs`
    - manual argument parsing;
    - routing for `export` and `import` commands.
- `ExportadorTabelaFirebird`
    - writes SQL header;
    - executes SELECT;
    - generates INSERTs;
    - controls commits/progress/error logging.
- `ConstrutorInsert`
    - serializes Firebird types to SQL literals;
    - BLOBs (Hex/Base64) and text.
- `ImportadorSql`
    - streaming SQL parser (comments, strings, delimiter via `SET TERM`);
    - transaction control and metrics;
    - disabling/re-enabling indexes.
- `ExecutorSql`
    - executes individual commands (including COMMIT and SET).

## 4) Implementation Rules for AI
- Prefer small, localized changes per module.
- Always handle errors with enough context for diagnosis (line/command/file).
- Avoid introducing new SQL concatenation unless it is truly necessary; when unavoidable, validate inputs.
- Ensure new options:
    - have a sensible default;
    - are documented in the help and README;
    - are considered in export/import when applicable.
- For large logs, use append mode in a dedicated file and short console messages.

## 5) Known Technical Risks (do not ignore)
- `--where` remains Firebird free-form SQL; although there is validation for dangerous tokens, full syntax validation depends on the database.
- Import/export operations on very large files require monitoring disk space and log size.
- When adding new CLI parameters, keep parser, help (`Program.cs`), and `README.md` in sync.

When the task touches these points, prioritize the fix with minimal impact and regression testing.

## 6) Checklist Before Finishing
1. Does the change address exactly what the user asked for?
2. Does the flow still operate in streaming mode?
3. Was charset/encoding preserved?
4. Do the CLI help and README need updates?
5. Are error logs still useful?
6. Is there a risk of breaking import/export for large files?

## 7) Minimum Validation Checklist
- Local build without errors.
- Run at least one affected flow:
    - simple export of a small table;
    - simple import with `SET NAMES` and `COMMIT`.
- Confirm that error log files are still generated when expected.

## 8) Scope and Style
- Keep names and conventions in Portuguese, as in the current codebase.
- Avoid adding external dependencies without a strong justification.
- Avoid comments in code; prefer self-explanatory code.

## 9) Code Quality and Maintainability (Supplementary)

This section adds explicit rules to prevent recurring problems seen in AI-generated code, such as duplication, excessive coupling, and poor readability.

### 9.1) Avoid Duplication (DRY)
- Before creating new logic, ALWAYS check whether similar implementation already exists in the project.
- Do not duplicate:
    - SQL value serialization;
    - charset handling;
    - command parsing logic;
    - error handling.
- If duplication exists:
    - extract a reusable method or class;
    - keep responsibilities clear.

### 9.2) Single Responsibility (SRP)
- Each class must have a single reason to change.
- Avoid classes that:
    - do parsing + execution + logging at the same time;
    - mix business rules with IO (console/file).
- Separate clearly:
    - argument parsing;
    - rule execution;
    - output/log writing.
- In the CLI, also keep these responsibilities separate:
    - parsing;
    - validation;
    - batch/wildcard resolution;
    - help/user output;
    - result printing.

### 9.3) Size and Complexity
- Avoid long methods (> ~50 lines).
- Avoid blocks with multiple nested `if/else` levels.
- When logic grows:
    - extract smaller functions;
    - use descriptive names.

### 9.4) Names and Readability
- Use explicit names (avoid unnecessary abbreviations).
- Names should reflect intent, not implementation.
- Avoid unnecessary comments - code should be self-explanatory.

### 9.5) Error Handling (Standardization)
- Never silently ignore exceptions.
- Always include context:
    - SQL command;
    - approximate line;
    - file.
- Avoid generic `catch (Exception)` without rethrow or structured logging.

### 9.6) Architectural Consistency
- Reuse existing structures before creating new ones:
    - `ExportadorTabelaFirebird`
    - `ImportadorSql`
    - `ExecutorSql`
- Do not create new "parallel services" that duplicate responsibilities.
- Keep the current folder organization pattern.
- If common text/language, batch pattern, or help logic appears, centralize it in `Cli/Common` before duplicating it across commands.

### 9.7) Dependencies
- Do not add external libraries without a clear need.
- Prefer native .NET libraries.
- Any new dependency must:
    - solve a real problem;
    - be mentioned in the README.

### 9.8) CLI and User Experience
- Messages must be:
    - short;
    - clear;
    - consistent.
- Errors must indicate how to fix the issue.
- Do not change existing flag behavior unless necessary.

### 9.9) Safe Refactoring
- Refactor only when it:
    - reduces duplication;
    - improves clarity;
    - reduces coupling.
- Avoid refactoring together with a large functional change.
- Prefer small, reviewable changes.

### 9.10) Quality Checklist (Mandatory)
Before finishing, validate:

1. Was any duplicate code introduced?
2. Did any function become too long or too complex?
3. Was existing code reused correctly?
4. Is the code easy to understand without comments?
5. Is error handling consistent?
6. Was any responsibility mixed improperly?
7. Did the change preserve the project's architectural pattern?
8. Did the change introduce new PT-BR text without accent review?

If any answer indicates a quality problem, fix it before concluding.

### 10) Language Policy (English by default, PT-BR when detected)

This project is international. The runtime and CLI default language is **English**. When `IdiomaSaidaDetector` identifies `pt-BR`, the same message must be shown in Portuguese with correct accenting.
Internal documentation may be in Portuguese.

#### 10.1) Where to Use ENGLISH
- CLI messages when the culture is not `pt-BR`
- Operational logs
- Main README
- Command and flag names

Example:
- "Error executing command"
- "File not found"

#### 10.2) Where to Use PORTUGUESE
- CLI messages when `pt-BR` is detected
- Names of classes, methods, variables, and files
- Internal documentation (for example: `AGENTS.md`, explanatory comments when truly necessary)
- Support materials for Brazilian developers

#### 10.3) Localization Rule
- Every new user-facing message must have an English and a Portuguese variant.
- English is the fallback default.
- The Portuguese variant must use correct accenting and natural language.
- When the message supports localization, use the shared localization helper instead of concatenating raw text.

Example:
- `CliText.Texto(idioma, "Invalid option.", "Opção inválida.")`

#### 10.4) Portuguese Quality Rule
When Portuguese is used:
- Always use correct accenting
- Do not generate accentless text (for example: "acao", "informacao")
- Avoid basic grammar mistakes
- Keep language clear and natural
- PT-BR text must not be generated without accents, cedilla, or simplified spelling.
- Before finishing, review new strings with focus on words like `nao`, `opcao`, `padrao`, `relatorio`, `invalido`.

Example:

Wrong:
- "Erro na execucao do comando"
- "Arquivo nao encontrado"

Correct:
- "Erro na execução do comando"
- "Arquivo não encontrado"

#### 10.5) Consistency (critical rule)
- Never mix languages in the same context.
- Runtime messages must follow the detected culture.
- Documentation may be bilingual, but each section must be clearly written in the chosen language.
- Do not partially translate messages or names.

#### 10.6) Language Checklist
Before finishing, validate:

1. Are the code and CLI using English by default and PT-BR only when detected?
2. Did any Portuguese text appear without correct accenting?
3. Do localized texts use the shared helper?
4. Is there any language mixing in the same context?
5. Are the documentation and help still aligned with the current behavior?

If there are inconsistencies, fix them before concluding.

### 11) Git Commit Policy

- Commit messages must ALWAYS be in English.
- Use a short imperative summary (example: "Add DDL severity override config").
- Avoid vague messages like "fixes", "update", or "adjustments".
- If there is more than one commit, keep scope and message consistent.
