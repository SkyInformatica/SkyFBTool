# `import` command

## What it does
Executes SQL scripts on Firebird using streaming parsing (line-by-line), with support for:
- `SET NAMES` detection
- `SET TERM` delimiter changes
- command-level execution with optional best-effort continuation
- per-run execution log file

Use `import` for data loads, schema scripts, and controlled SQL replay.

## How to use
```powershell
SkyFBTool import --database PATH.fdb --input FILE.sql [options]
SkyFBTool import --database PATH.fdb --inputs-batch "C:\exports\*.sql" [options]
```

## All options
- `--database`: target Firebird database path.
- `--input`: input SQL file (single-file mode).
- `--script`: explicit alias for `--input`.
- `--inputs-batch`: wildcard pattern for multiple SQL files (batch mode).
- `--input-batch`: alias for `--inputs-batch`.
- `--scripts-batch`: alias for `--inputs-batch`.
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--progress-every`: prints progress every N processed lines/commands.
- `--continue-on-error`: keeps running after SQL command failures (best-effort mode).

## Rules
- Use only one input mode per run:
  - single file: `--input/--script`
  - batch: `--inputs-batch` (or aliases)
- Do not mix single-file and batch options in the same execution.

## Execution model (important)
- Parser is streaming and SQL-aware:
  - ignores comments (`-- ...`, `/* ... */`)
  - respects string literals (including escaped quotes)
  - supports dynamic delimiter changes via `SET TERM`
- Commands are executed one-by-one, in transaction context.
- Table indexes can be temporarily managed internally during command execution flow.
- Final summary includes:
  - total processed lines
  - total executed commands
  - elapsed time
  - average command throughput

## Error handling semantics
- Without `--continue-on-error`:
  - first SQL execution error aborts the file with exception.
- With `--continue-on-error`:
  - failed commands are logged and execution continues.
  - final run status can still contain errors.

## Batch summary semantics
In batch mode, final summary distinguishes:
- `Succeeded`: file completed without SQL command errors.
- `Succeeded with errors`: file completed, but one or more SQL commands failed under `--continue-on-error`.
- `Failed`: file aborted by fatal error.

## Execution log
- A log file is always generated per execution with unique name:
  - `*_import_log_*.log`
- Log contains start/end, command-level errors, and final status.
- In operational incidents, this file is the primary audit source.

## Operational recommendations
- For high-risk scripts, run first in staging with same engine version/charset.
- In production, prefer:
  - explicit backup/restore point
  - controlled windows
  - `--continue-on-error` only when partial completion is acceptable
- For large scripts:
  - use `--progress-every` for observability
  - split script files at source if rollback/retry granularity is required

## Examples
```powershell
SkyFBTool import --database "C:\data\erp.fdb" --input "C:\exports\customers.sql"
SkyFBTool import --database "C:\data\erp.fdb" --script ".\sql\patch.sql" --progress-every 1000
SkyFBTool import --database "C:\data\erp.fdb" --inputs-batch "C:\exports\*.sql" --continue-on-error
```
