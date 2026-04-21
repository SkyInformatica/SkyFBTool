# SkyFBTool

English | [Português (Brasil)](./README.pt-BR.md)

SkyFBTool is a .NET 8 CLI for Firebird data export/import (2.5 / 3.0 / 4.0 / 5.0), focused on large datasets, streaming execution, and charset-safe workflows.

## What's New

- [CHANGELOG.md](./CHANGELOG.md)
- [Releases](https://github.com/SkyInformatica/SkyFBTool/releases)

## Main Features

- `export` and `import` commands
- Streaming export/import for large SQL files
- `--filter`, `--filter-file`, and advanced `--query-file`
- Target table remap with `--target-table`
- `--blob-format` (`Hex` or `Base64`)
- Configurable `--commit-every` and `--progress-every`
- File splitting with `--split-size-mb` (default: 100 MB)
- Legacy charset mode for `CHARSET NONE` via `--legacy-win1252`
- Warning for large filter/query files (> 64 KB)

## Usage

```powershell
SkyFBTool export [options]
SkyFBTool import [options]
```

### Export example

```powershell
SkyFBTool export --database "C:\data\sample.fdb" --table "SAMPLE_TABLE" --output "C:\exports\" --commit-every 10000
```

### Import example

```powershell
SkyFBTool import --database "C:\data\sample.fdb" --input "C:\exports\sample_table.sql" --continue-on-error
```

## Key Export Options

- `--database` Firebird database path
- `--table` source table
- `--target-table` target table in generated `INSERT`s
- `--output` output file or directory
- `--charset` `WIN1252 | ISO8859_1 | UTF8 | NONE`
- `--filter` simple condition (optional)
- `--filter-file` read simple condition from file
- `--query-file` read full `SELECT` from file (advanced mode)
- `--split-size-mb` output split size in MB (`0` disables)
- `--legacy-win1252` legacy mode for `CHARSET NONE`

Rules:
- Do not combine `--query-file` with `--filter` or `--filter-file`.
- `--query-file` must contain a full `SELECT`.
- `--filter` accepts optional `WHERE` prefix (automatically removed).

## Tests

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

Integration tests:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Documentation Standard

- [DOCS_STANDARD.md](./DOCS_STANDARD.md)

## Disclaimer

SkyFBTool is provided under the MIT license, "AS IS", without warranties of any kind.

The authors are not liable for:
- data loss
- database corruption
- execution failures
- direct or indirect damages
- misuse
- third-party impacts

Always validate in a staging/homologation environment before production use.

## License

MIT
