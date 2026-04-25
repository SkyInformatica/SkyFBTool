# `import` command

## What it does
Executes a SQL file on Firebird using streaming, with progress reporting and optional continue-on-error.

## How to use
```powershell
SkyFBTool import --database PATH.fdb --input FILE.sql [options]
```

## Main options
- `--database`: database path.
- `--input`: input SQL file.
- `--host`, `--port`, `--user`, `--password`: connection settings.
- `--progress-every`: progress interval.
- `--continue-on-error`: keeps running after command failures.

## Examples
```powershell
SkyFBTool import --database "C:\data\erp.fdb" --input "C:\exports\customers.sql"
SkyFBTool import --database "C:\data\erp.fdb" --input "C:\exports\orders.sql" --progress-every 5000 --continue-on-error
```

## Output example
```text
Starting import...
Lines: 50,000 | Commands: 49,990 | Speed: 2,100 cmd/s
Import finished.
Total lines processed : 52,143
Total commands executed: 51,998
Total execution time   : 00:00:24.318
Average speed          : 2,137.42 commands/second
```
