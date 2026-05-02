# `ddl-analyze` Severity and Validation Criteria

This document describes exactly which validations `ddl-analyze` runs and how each finding is classified by severity.

## Analysis scope

- File mode (`--input/--source`): structural schema validations.
- Database mode (`--database`): structural validations + operational findings (`MON$`).
- Batch mode (`--databases-batch`): same behavior as database mode, executed per `.fdb`.

## Severity levels

- `critical`: severe structural inconsistency or high integrity risk.
- `high`: significant quality/operational risk without direct immediate corruption evidence.
- `medium`: relevant performance/operational risk that should be tracked and mitigated.
- `low`: simplification/optimization opportunity with low immediate risk.

## Severity override rule

- Default severity per code can be overridden with `--severity-config`.
- Reference format: `docs/examples/ddl-severity.sample.json`.
- Accepted values: `critical`, `high`, `medium`, `low`.

## Structural validation matrix

| Code | Default severity | What it validates | Current rule |
|---|---|---|---|
| `TABELA_SEM_COLUNAS` | `critical` | Table with no columns | Column count is zero |
| `COLUNA_DUPLICADA` | `critical` | Duplicated column in same table | Group by column name (case-insensitive), count > 1 |
| `TIPO_DESCONHECIDO` | `high` | Unmapped SQL type | Type token starts with `TYPE_` |
| `TABELA_SEM_PK` | `high` | Table without PK | Primary key is missing |
| `PK_SEM_COLUNAS` | `critical` | PK without columns | PK column list is empty |
| `PK_REFERENCIA_COLUNA_INEXISTENTE` | `critical` | PK references missing column | PK column not found in table column map |
| `FK_SEM_COLUNAS` | `critical` | FK without local/reference columns | Local and/or referenced column list is empty |
| `FK_CARDINALIDADE_INVALIDA` | `critical` | FK local/reference cardinality mismatch | Local and referenced column counts differ |
| `FK_COLUNA_LOCAL_INEXISTENTE` | `critical` | FK uses missing local column | FK local column not found in table |
| `FK_TABELA_REFERENCIA_INEXISTENTE` | `critical` | FK references missing table | Referenced table not found in table map |
| `FK_COLUNA_REFERENCIA_INEXISTENTE` | `critical` | FK references missing target column | FK referenced column not found |
| `FK_SEM_INDICE_COBERTURA` | `medium` | FK without local covering index | No FK support index is present and no regular index covers FK column prefix |
| `INDICE_SEM_COLUNAS` | `high` | Index without columns | Index column list is empty |
| `INDICE_COLUNA_INEXISTENTE` | `high` | Index references missing column | Index column not found |
| `INDICE_DUPLICADO` | `low` | Indexes with same functional signature | Same signature (`U/N`, `A/D`, ordered column list) |
| `INDICE_REDUNDANTE_PREFIXO` | `medium` | Potentially redundant prefix index | Short index is prefix of larger index, same direction, both non-unique |
| `FK_DUPLICADA` | `low` | FKs with same functional signature | Same signature (local columns, referenced table/columns, update/delete rules) |
| `OPERACIONAL_VOLUME_PRIORIDADE_ALTA` | `high` | High-volume table with concentrated findings | Estimated rows >= 10,000,000 and findings in table >= 3 |
| `OPERACIONAL_VOLUME_PRIORIDADE_MEDIA` | `medium` | Relevant-volume table with repeated findings | Estimated rows >= 1,000,000 and findings in table >= 2 |
| `OPERACIONAL_VOLUME_PRIORIDADE_BAIXA` | `low` | Medium-volume table with findings | Estimated rows >= 500,000 and findings in table >= 1 |

## Operational validation matrix (`MON$`) - `--database` only

| Code | Default severity | What it validates | Current threshold | Practical meaning |
|---|---|---|---|---|
| `OPERACIONAL_GAP_OIT_OAT_ELEVADO` | `critical` | Transaction retention pressure from OAT-OIT gap | `OAT - OIT >= 200000` | Strong sign of blocked garbage collection; high risk of sustained degradation and urgent investigation needed. |
| `OPERACIONAL_GAP_OIT_OAT_ACIMA_DO_ESPERADO` | `high` | Transaction retention pressure above expected | `OAT - OIT >= 50000` | Cleanup is lagging behind workload; likely long transactions or housekeeping cadence issues. |
| `OPERACIONAL_GAP_OIT_OAT_MODERADO` | `medium` | Moderate transaction retention pressure | `OAT - OIT >= 10000` | Early pressure signal; monitor trend and prevent growth before it becomes critical. |
| `OPERACIONAL_GAP_OAT_OST_ELEVADO` | `high` | High snapshot backlog from OST-OAT gap | `OST - OAT >= 200000` | Long snapshot readers are likely retaining old versions; read/reporting profile should be reviewed. |
| `OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO` | `medium` | Snapshot backlog above expected | `OST - OAT >= 50000` | Snapshot retention is above healthy baseline; monitor and tune long-running reads. |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA_CRITICA` | `critical` | Critical long active transaction | age >= `720` minutes | A transaction has been open for many hours; immediate action is required to avoid retention and lock pressure. |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA` | `high` | Long active transaction | age >= `120` minutes | Transaction duration is already risky for normal operations and should be investigated soon. |
| `OPERACIONAL_TRANSACAO_ATIVA_ACIMA_DO_ESPERADO` | `medium` | Active transaction duration above expected | age >= `30` minutes | Transaction is longer than expected baseline; review workload pattern and watch trend. |

### What each MON$ metric means

- `OIT` (`mon$oldest_transaction`): oldest transaction id that still cannot be garbage-collected.
- `OAT` (`mon$oldest_active`): oldest currently active transaction id.
- `OST` (`mon$oldest_snapshot`): oldest snapshot transaction still holding old record versions.
- `NXT` (`mon$next_transaction`): next transaction id to be assigned.

### How gap-based findings are interpreted

- `OAT - OIT` (retention pressure):
  - Large values usually mean old transactions are preventing cleanup of record versions.
  - Practical impact: garbage accumulation, longer back-version chains, and performance degradation over time.
- `OST - OAT` (snapshot backlog):
  - Large values usually indicate long-running snapshot readers keeping old versions visible.
  - Practical impact: slower cleanup and increased storage/IO pressure.

### How long-active-transaction findings are calculated

- Query used: `MIN(mon$timestamp)` from `mon$transactions` where `mon$state = 1`, excluding current connection.
- The analyzer computes `DateTime.UtcNow - MIN(mon$timestamp)` and classifies by threshold:
  - `>= 720 min` -> `critical`
  - `>= 120 min` -> `high`
  - `>= 30 min` -> `medium`

### Practical reading examples

- Example A: `OIT=1000`, `OAT=260000` -> `OAT - OIT = 259000` -> `OPERACIONAL_GAP_OIT_OAT_ELEVADO` (`critical`).
- Example B: `OAT=500000`, `OST=560500` -> `OST - OAT = 60500` -> `OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO` (`medium`).
- Example C: oldest active transaction started 3h10 ago -> `190 min` -> `OPERACIONAL_TRANSACAO_ATIVA_LONGA` (`high`).

### Scope and limitations

- Operational checks are emitted only in DB mode (`--database`, and per DB in `--databases-batch`).
- File mode (`--input/--source`) has no live transaction context; therefore no `OPERACIONAL_*` findings.
- If MON$ collection fails (permissions/access/query), structural findings are still produced and the report marks operational analysis as `unavailable`, with the reason in the summary.

### Operational metrics source

- `mon$database`: `mon$oldest_transaction`, `mon$oldest_active`, `mon$oldest_snapshot`, `mon$next_transaction`
- `mon$transactions`: `MIN(mon$timestamp)` for active transactions (`mon$state = 1`), excluding current connection.

## Classification notes (best-practice alignment)

- `critical` findings represent structural consistency errors (invalid/incomplete schema) or extreme operational signals.
- `high` findings indicate substantial stability and data-governance risk.
- `medium` findings indicate conditions that commonly degrade performance/operations and should have a remediation plan.
- `low` findings represent redundancy/optimization opportunities and should be validated with real query plans/workload before removal.

## Interpretation notes

- In `--input/--source` mode, `MON$` is not queried; therefore `OPERACIONAL_*` codes are not emitted.
- If operational collection fails in `--database` mode (permissions/access/query failure), structural analysis still completes and the report is generated with operational analysis marked as `unavailable`.
- `FK_SEM_INDICE_COBERTURA` is not emitted when the FK has a support index bound to the constraint (for example, `RDB$RELATION_CONSTRAINTS.RDB$INDEX_NAME` in DB mode, or matching FK/constraint index in SQL snapshot mode).
- Volume-priority findings are emitted only in DB mode and use index-based estimated row count by default (best-effort); exact `COUNT(*)` is optional via `--volume-count-exact on`.
