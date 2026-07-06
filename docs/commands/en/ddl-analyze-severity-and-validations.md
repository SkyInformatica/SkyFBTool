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

| Code | Default severity | What it validates | Current rule | Practical explanation |
|---|---|---|---|---|
| `TABELA_SEM_COLUNAS` | `critical` | Table with no columns | Column count is zero | Table exists in schema metadata but has no valid columns. Ex.: `CREATE TABLE LOG_AUDIT ();` |
| `COLUNA_DUPLICADA` | `critical` | Duplicated column in same table | Group by column name (case-insensitive), count > 1 | Same column name was declared multiple times in one table. Ex.: `CUSTOMERS(ID INT, NAME VARCHAR(60), NAME VARCHAR(80))`. |
| `TIPO_DESCONHECIDO` | `high` | Unmapped SQL type | Type token starts with `TYPE_` | Column type token is not recognized/mapped for Firebird. Ex.: `TOTAL TYPE_99` in a billing table. |
| `TABELA_SEM_PK` | `high` | Table without PK | Primary key is missing | Table has no reliable primary identifier. Ex.: `ORDERS` with millions of rows and no `PRIMARY KEY`. |
| `PK_SEM_COLUNAS` | `critical` | PK without columns | PK column list is empty | PK constraint exists but has no valid bound columns. Ex.: `PK_ORDERS` created without bound columns. |
| `PK_REFERENCIA_COLUNA_INEXISTENTE` | `critical` | PK references missing column | PK column not found in table column map | PK points to a non-existent column. Ex.: PK uses `ORDER_ID`, table only has `ORDER_CODE`. |
| `FK_SEM_COLUNAS` | `critical` | FK without local/reference columns | Local and/or referenced column list is empty | FK has incomplete relationship mapping. Ex.: `FK_ORDER_ITEMS` declared without field mapping. |
| `FK_CARDINALIDADE_INVALIDA` | `critical` | FK local/reference cardinality mismatch | Local and referenced column counts differ | Local and referenced FK column counts do not match. Ex.: local FK `(COMPANY_ID, ORDER_ID)` to parent PK `(ORDER_ID)`. |
| `FK_COLUNA_LOCAL_INEXISTENTE` | `critical` | FK uses missing local column | FK local column not found in table | FK uses a column that is missing in child table. Ex.: FK in `ORDER_ITEMS` uses `BRANCH_ID`, but column is missing. |
| `FK_TABELA_REFERENCIA_INEXISTENTE` | `critical` | FK references missing table | Referenced table not found in table map | FK points to a parent table not present in analyzed schema. Ex.: FK references `CUSTOMER_MASTER`, but only `CUSTOMERS` exists. |
| `FK_COLUNA_REFERENCIA_INEXISTENTE` | `critical` | FK references missing target column | FK referenced column not found | FK points to missing column in parent table. Ex.: FK targets `CUSTOMERS(CUSTOMER_ID)`, parent has `ID`. |
| `FK_SEM_INDICE_COBERTURA` | `medium` | FK without local covering index | No FK support index is present and no regular index covers FK column prefix | FK has no useful index for join/referential checks. Ex.: `ORDERS.CUSTOMER_ID` FK without index starting with `CUSTOMER_ID`. |
| `INDICE_SEM_COLUNAS` | `high` | Index without columns | Index column list is empty | Index was defined with no valid columns. Ex.: `IDX_MOV` created empty by incomplete script. |
| `INDICE_COLUNA_INEXISTENTE` | `high` | Index references missing column | Index column not found; expression index items are ignored | Index includes column absent from table. Ex.: index on `SALES(ISSUE_DATE)` where `ISSUE_DATE` is missing. Expression indexes such as `UPPER(DESCRIPTION)` are not reported as missing columns. |
| `INDICE_DUPLICADO` | `low` | Indexes with same functional signature | Same signature (`U/N`, `A/D`, ordered column list) | Two indexes provide the same effective behavior. Ex.: `IDX_A` and `IDX_B` on `(CUSTOMER_ID, DATE)` non-unique/ASC. |
| `INDICE_REDUNDANTE_PREFIXO` | `medium` | Potentially redundant prefix index | Short index is prefix of larger index, same direction, both non-unique | Shorter index is likely redundant. Ex.: `(CUSTOMER_ID)` and `(CUSTOMER_ID, DATE)` same direction. |
| `FK_DUPLICADA` | `low` | FKs with same functional signature | Same signature (local columns, referenced table/columns, update/delete rules) | Different FK constraints repeat same relationship rule. Ex.: `FK_ORD_CUST_1` and `FK_ORD_CUST_2` with same fields/rules. |
| `PROCEDURE_SEM_CORPO` | `critical` | Procedure without valid PSQL body | Procedure source is empty, lacks `AS`/`BEGIN`, or has only comments inside `BEGIN`/`END` | Procedure metadata is incomplete or inert and may break DDL generation or reapply. Ex.: `SP_RECALC_TOTALS` extracted without executable body text. |
| `FUNCTION_SEM_CORPO` | `critical` | Function without valid PSQL body | Function source is empty, lacks `AS`/`BEGIN`, or has only comments inside `BEGIN`/`END` | Function metadata is incomplete or inert and may break DDL generation or reapply. Ex.: `FN_NORMALIZE_NAME` extracted without executable body text. |
| `TRIGGER_SEM_CORPO` | `critical` | Trigger without valid PSQL body | Trigger source is empty, lacks `AS`/`BEGIN`, or has only comments inside `BEGIN`/`END` | Trigger metadata is incomplete or inert and may break DDL generation or reapply. Ex.: `TRG_ORDERS_BI` extracted without executable body text. |
| `PROCEDURE_SOMENTE_SUSPEND` | `high` | Procedure with inert PSQL body | Body contains only `SUSPEND` after comments and separators are ignored | Procedure exists and compiles, but returns an empty/unassigned row without useful logic. Ex.: `BEGIN SUSPEND; END`. |
| `FUNCTION_SOMENTE_SUSPEND` | `high` | Function with inert PSQL body | Body contains only `SUSPEND` after comments and separators are ignored | Function metadata has an executable block, but no useful calculation or return logic. |
| `TRIGGER_SOMENTE_SUSPEND` | `high` | Trigger with inert PSQL body | Body contains only `SUSPEND` after comments and separators are ignored | Trigger exists but has no meaningful action. |
| `OPERACIONAL_VOLUME_PRIORIDADE_ALTA` | `high` | High-volume table with concentrated findings | Estimated rows >= 10,000,000 and findings in table >= 3 | Very large table with multiple findings; high blast radius. Ex.: `STOCK_MOV` with 25M rows and 4 structural findings. |
| `OPERACIONAL_VOLUME_PRIORIDADE_MEDIA` | `medium` | Relevant-volume table with repeated findings | Estimated rows >= 1,000,000 and findings in table >= 2 | Large table with recurring findings; relevant priority. Ex.: `INVOICE_ITEMS` with 2.3M rows and 2 findings. |
| `OPERACIONAL_VOLUME_PRIORIDADE_BAIXA` | `low` | Medium-volume table with findings | Estimated rows >= 500,000 and findings in table >= 1 | Mid-size table with finding; preventive prioritization. Ex.: `EVENT_LOG` with 700k rows and 1 finding. |

## Field compatibility validation matrix (`CAMPO_*`)

These codes are emitted by the field-compatibility validator and are part of the same `ddl-analyze` findings list.

| Code | Default severity | What it validates | Current rule | Practical explanation |
|---|---|---|---|---|
| `CAMPO_TIPO_VAZIO` | `critical` | Missing/empty SQL type | `TipoSql` is null, empty, or whitespace | Column/domain metadata has no valid type for safe DDL generation. |
| `CAMPO_TAMANHO_EFETIVO_EXCEDIDO` | `critical` | Effective `CHAR/VARCHAR` size exceeds Firebird limit | `(declared length * bytes per character) > 32765` | Text field can exceed the real byte limit depending on charset. |
| `CAMPO_PRECISAO_NUMERICA_INVALIDA` | `critical` | Invalid `NUMERIC/DECIMAL` precision/scale | Precision outside `1..38`, scale `< 0`, or scale `> precision` | Invalid numeric definition for consistent DDL generation. |
| `CAMPO_PRECISAO_NUMERICA_INCOMPATIVEL` | `critical` | Precision incompatible with target version | Firebird `< 4` and precision `> 18` | Precision requires newer Firebird capabilities. |
| `CAMPO_TIPO_INCOMPATIVEL_VERSAO` | `critical` | Type incompatible with target version | Type requires minimum major version (for example `BOOLEAN`>=3; `DECFLOAT/INT128/TIME WITH TIME ZONE/TIMESTAMP WITH TIME ZONE`>=4) | Type may be valid in newer versions but incompatible with target legacy environment. |

## Operational validation matrix (`MON$`) - `--database` only

| Code | Default severity | What it validates | Current threshold | Practical meaning |
|---|---|---|---|---|
| `OPERACIONAL_GAP_OIT_OAT_ELEVADO` | `critical` | Transaction retention pressure from OAT-OIT gap | `OAT - OIT >= 200000` | Critical transaction-retention backlog; immediate degradation risk. Ex.: `OIT=1000`, `OAT=260500` (gap 259500). |
| `OPERACIONAL_GAP_OIT_OAT_ACIMA_DO_ESPERADO` | `high` | Transaction retention pressure above expected | `OAT - OIT >= 50000` | High transaction pressure above healthy baseline. Ex.: `OIT=50000`, `OAT=125500` (gap 75500). |
| `OPERACIONAL_GAP_OIT_OAT_MODERADO` | `medium` | Moderate transaction retention pressure | `OAT - OIT >= 10000` | Early retention pressure signal, still manageable with quick action. Ex.: `OIT=880000`, `OAT=893500` (gap 13500). |
| `OPERACIONAL_GAP_OAT_OST_ELEVADO` | `high` | High snapshot backlog from OST-OAT gap | `OST - OAT >= 200000` | Long snapshot readers are retaining old versions for too long. Ex.: `OAT=320000`, `OST=540500` (gap 220500). |
| `OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO` | `medium` | Snapshot backlog above expected | `OST - OAT >= 50000` | Snapshot retention is above operational baseline. Ex.: `OAT=700000`, `OST=761000` (gap 61000). |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA_CRITICA` | `critical` | Critical long active transaction | age >= `720` minutes | An active transaction has remained open for many hours. Ex.: reconciliation session open for 13h without commit/rollback. |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA` | `high` | Long active transaction | age >= `120` minutes | Long active transaction with relevant retention/lock risk. Ex.: integration process with a transaction open for 2h40. |
| `OPERACIONAL_TRANSACAO_ATIVA_ACIMA_DO_ESPERADO` | `medium` | Active transaction duration above expected | age >= `30` minutes | Transaction duration is above expected workload baseline. Ex.: update job with active transaction for 45 min. |

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
