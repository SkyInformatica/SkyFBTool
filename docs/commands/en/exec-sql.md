# `exec-sql` command

## What it does
Executes an SQL script against Firebird using the same execution engine as `import`.

Use `exec-sql` when your intent is operational script execution (schema patch, data fix, maintenance script), not data export/import workflow.

## When to use
- DBA: controlled maintenance execution with explicit operational intent and post-run audit log.
- Developer: deterministic patch application during environment bootstrap or migration rehearsal.

## How to use
```powershell
SkyFBTool exec-sql --database PATH.fdb --script FILE.sql [options]
```

## All options
`exec-sql` uses the same parsing/execution engine as `import`, but only in single-file mode:
- `--database`: target Firebird database.
- `--input`: input SQL file.
- `--script`: explicit alias for `--input` (recommended for readability in maintenance context).
- `--host`: server host (default: `localhost`).
- `--port`: server port (default: `3050`).
- `--user`: user (default: `sysdba`).
- `--password`: password (default: `masterkey`).
- `--progress-every`: progress output interval (command-level observability only).
- `--continue-on-error`: continue after SQL command failures (best-effort execution).

## Rules and operational guidance
- Use only one input file per execution (`--input` or `--script`).
- Prefer `--script` in `exec-sql` runs so logs/commands clearly communicate maintenance intent.
- Use `--continue-on-error` only when partial execution is acceptable and post-run validation is planned.
- Always review the generated import log (`*_import_log_*.log`) when running with `--continue-on-error`.
- For high-risk scripts (DDL in production), run first in staging and keep explicit rollback strategy.

## Practical difference vs `import`
- `import`: positioned for SQL data/script ingestion workflows, including batch input mode.
- `exec-sql`: same engine, but operational naming focused on patch/maintenance script execution.

## Examples
```powershell
SkyFBTool exec-sql --database "C:\data\erp.fdb" --script ".\sql\patch_2026_04.sql"
SkyFBTool exec-sql --database "C:\data\erp.fdb" --script ".\sql\rebuild_indexes.sql" --progress-every 500
SkyFBTool exec-sql --database "C:\data\erp.fdb" --script ".\sql\best_effort_cleanup.sql" --continue-on-error
```
