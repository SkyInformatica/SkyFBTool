# `exec-sql` command

## What it does
Alias for `import`, typically used to execute maintenance/patch SQL scripts.

## How to use
```powershell
SkyFBTool exec-sql --database PATH.fdb --script FILE.sql [options]
```

## Main options
- `--script`: explicit alias for `--input`.
- Supports the same options as `import` (`--progress-every`, `--continue-on-error`, etc.).

## Examples
```powershell
SkyFBTool exec-sql --database "C:\data\erp.fdb" --script ".\sql\patch_2026_04.sql"
SkyFBTool exec-sql --database "C:\data\erp.fdb" --script ".\sql\rebuild_indexes.sql" --continue-on-error
```

## Output example
```text
Starting import...
Import finished.
Total commands executed: 182
```
