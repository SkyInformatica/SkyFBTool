English | [Português (Brasil)](../pt-BR/schema-governance.md)

# Schema Governance Model

SkyFBTool supports schema governance through reproducible artifacts, drift visibility, and human review loops.

## Governance Principles

- artifacts first: generate immutable snapshots before changes;
- drift visibility: compare source and target structures explicitly;
- staged promotion: review and apply in controlled environments;
- verification loop: validate convergence after apply.

## Main Building Blocks

1. `ddl-extract`  
   Produces `.sql` and normalized `.schema.json` snapshots.

2. `ddl-diff`  
   Detects structural differences and generates SQL/JSON/HTML outputs for review.

3. `ddl-analyze`  
   Classifies structural risk before or after planned changes.

## Human Review and Approval

- review generated SQL in staging;
- validate dependency order and rollback strategy;
- classify findings by severity and business impact;
- promote only after explicit approval.

## Anti-Drift Routine

1. Extract source/target snapshots.
2. Run `ddl-diff`.
3. Review and apply approved SQL.
4. Re-run `ddl-diff` until convergence.
5. Keep artifacts for traceability.
