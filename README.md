# SkyFBTool

English | [Português (Brasil)](./README.pt-BR.md)

SkyFBTool is an operational engineering platform for Firebird focused on resilient execution, structural governance, and risk mitigation in real production environments.  
It combines streaming data operations, schema governance workflows, and DDL risk analysis to support Firebird `2.5`, `3.0`, `4.0`, and `5.0`, including legacy charset scenarios.

## Project Vision

SkyFBTool is designed to make Firebird operations predictable, auditable, and safer over time:

- preventive engineering instead of reactive fixes;
- structural and operational signals in the same decision flow;
- reproducible artifacts for human review and controlled rollout;
- resilient execution patterns for large-volume and long-running operations.

## Why SkyFBTool

| Pillar | What it delivers |
|---|---|
| Operational engineering | Streaming execution, retry-aware processing, controlled commits, progress visibility |
| Schema governance | Snapshot extraction, drift detection, diff workflows, review-ready outputs |
| Risk mitigation | Severity-based findings, table-level prioritization, MON$ operational signals |
| Structural observability | Risk prioritization, operational signals, and consolidated table-level visibility |
| Legacy readiness | Firebird `2.5 -> 5.0`, charset-safe behavior, `CHARSET NONE` compatibility paths |

## Core Capabilities

### Resilient Operational Engineering

- resilient large-scale SQL movement and replay pipeline;
- SQL parser support for comments, strings, and `SET TERM`;
- execution controls: `--continue-on-error`, commit pacing, progress intervals;
- split output for large exports (`--split-size-mb`);
- operational logging for troubleshooting and audit.

### Structural Governance Process

- `ddl-extract`: normalized schema artifacts (`.sql` + `.schema.json`);
- `ddl-diff`: structural drift detection with SQL, JSON, and HTML outputs;
- snapshot-based review workflows for promotion and controlled synchronization.

### Structural Risk Analysis

- `ddl-analyze`: schema risk analysis with severity and prioritization;
- risk index and table-priority (`P0..P3`) orientation;
- operational checks from `MON$` in DB mode;
- optional volume-priority analysis (estimated or exact count mode).

## Differentiators

- built around real operational risk, not only command execution;
- combines structural and operational analysis in a single workflow;
- practical support for legacy Firebird constraints and charsets;
- outputs tailored for auditability and staged rollout decisions;
- severity-driven remediation prioritization for DBA workflows.

## Intentional Limits

Security-first operating model:

- generated SQL is never executed automatically;
- human review is a mandatory step in promotion flows;
- destructive operations must be explicit;
- continue-on-error is not the default behavior;
- artifacts must remain auditable.

## Real-World Use Cases

| Scenario | Tooling |
|---|---|
| Massive data recovery/movement | `export` + `import` |
| Structural risk audit | `ddl-analyze` |
| Environment drift detection | `ddl-diff` |
| DDL governance baseline | `ddl-extract` |
| Operational script execution | `exec-sql` |

## Supported Critical Scenarios

Supported scenarios:

- databases with tens of millions of records;
- long-running operations;
- legacy Firebird environments;
- structural drift across environments;
- operational rollback/re-execution;
- structural integrity troubleshooting;
- audit pipeline workflows.

## Conceptual Architecture

```text
ddl-extract
    ↓
ddl-diff
    ↓
ddl-analyze
    ↓
review/approval
    ↓
exec-sql / import
    ↓
revalidation
```

Quick summary:
- `ddl-extract` creates the environment structural baseline;
- `ddl-diff` identifies drift between source and target;
- `ddl-analyze` prioritizes risks before applying changes;
- human validation/review acts as the control mechanism before execution;
- `exec-sql` or `import` executes the planned change;
- revalidation confirms structural convergence and risk reduction.

## Recommended Flows

### 1) Data migration workflow (DBA/operations)
1. `export` from source table/query.
2. Review generated SQL and split/charset settings.
3. `import` into target with progress/log monitoring.
4. Validate execution summary and logs.

### 2) Schema promotion workflow (DBA + dev)
1. `ddl-extract` from source and target.
2. `ddl-diff` to generate SQL/JSON/HTML artifacts.
3. Review SQL and HTML report in staging.
4. Apply approved SQL and re-run `ddl-diff` for convergence.

### 3) Risk triage workflow (DBA)
1. Run `ddl-analyze` (prefer `--database` when possible).
2. Start from prioritized tables in HTML report.
3. Remediate `critical/high`, then `medium`.
4. Keep `low` findings as optimization backlog after plan validation.

### 4) Operational execution workflow
1. Execute maintenance scripts with `exec-sql`/`import`.
2. Track progress and command-level logs.
3. Use `--continue-on-error` only when continuity is explicitly required.

## Documentation Map

### Command Documentation

#### Data operations
- `create-db`: [docs/commands/en/create-db.md](./docs/commands/en/create-db.md)
- `export`: [docs/commands/en/export.md](./docs/commands/en/export.md)
- `import`: [docs/commands/en/import.md](./docs/commands/en/import.md)
- `exec-sql`: [docs/commands/en/exec-sql.md](./docs/commands/en/exec-sql.md)

#### Schema engineering
- `ddl-extract`: [docs/commands/en/ddl-extract.md](./docs/commands/en/ddl-extract.md)
- `ddl-diff`: [docs/commands/en/ddl-diff.md](./docs/commands/en/ddl-diff.md)
- `ddl-analyze`: [docs/commands/en/ddl-analyze.md](./docs/commands/en/ddl-analyze.md)

#### Technical criteria
- `ddl-analyze` severity/validation matrix: [docs/commands/en/ddl-analyze-severity-and-validations.md](./docs/commands/en/ddl-analyze-severity-and-validations.md)

### Concept Documentation

- Risk mitigation model: [docs/concepts/en/risk-mitigation.md](./docs/concepts/en/risk-mitigation.md)
- Schema governance model: [docs/concepts/en/schema-governance.md](./docs/concepts/en/schema-governance.md)
- Operational resilience model: [docs/concepts/en/operational-resilience.md](./docs/concepts/en/operational-resilience.md)
- Firebird compatibility model: [docs/concepts/en/firebird-compatibility.md](./docs/concepts/en/firebird-compatibility.md)
- Testing and validation strategy: [docs/concepts/en/testing-and-validation-strategy.md](./docs/concepts/en/testing-and-validation-strategy.md)

## Quick Start

```powershell
SkyFBTool export [options]
SkyFBTool create-db [options]
SkyFBTool import [options]
SkyFBTool exec-sql [options]
SkyFBTool ddl-extract [options]
SkyFBTool ddl-diff [options]
SkyFBTool ddl-analyze [options]
```

## Operational References

- Changelog: [CHANGELOG.md](./CHANGELOG.md)
- Releases: [GitHub Releases](https://github.com/SkyInformatica/SkyFBTool/releases)
- Documentation standard: [DOCS_STANDARD.md](./DOCS_STANDARD.md)
- Sample severity override schema: `docs/examples/ddl-severity.sample.json`
- Reproducible analyze samples: `docs/examples/ddl-analyze-sample*.{sql,json,html}`

## Tests

Operational test and validation model: [docs/concepts/en/testing-and-validation-strategy.md](./docs/concepts/en/testing-and-validation-strategy.md)

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

Integration tests:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Automated Releases

This repository includes a GitHub Actions release pipeline at `.github/workflows/release.yml`.

- Trigger: tag push in `v*` format (example: `v0.1.0`)
- Pipeline: restore, build, test, publish (`win-x64`, `linux-x64`)
- Output: GitHub Release with compiled artifacts (`.tar.gz`)

Tag example:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Dependencies

- `Scriban` is used to render `ddl-analyze` HTML reports from templates.

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
