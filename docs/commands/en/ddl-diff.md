# `ddl-diff` command

## What it does
Compares two schema inputs and generates:
- adjustment SQL (`.sql`) to move target toward source
- structured diff (`.json`) for tooling/auditing
- visual report (`.html`) for human review

`ddl-diff` is designed for controlled schema synchronization workflows (promotions, audits, migration planning).

## When to use
- DBA: assess schema drift and generate reviewed adjustment SQL before rollout.
- Developer: validate migration impact and keep source/target model alignment explicit.

## How to use
```powershell
SkyFBTool ddl-diff --source SOURCE --target TARGET --output PREFIX
```

## All options
- `--source`: source input (`.schema.json` or `.sql`).
- `--source-ddl`: alias for `--source`.
- `--target`: target input (`.schema.json` or `.sql`).
- `--target-ddl`: alias for `--target`.
- `--output`: output prefix/file base/directory.

## Rules and operational guidance
- Keep source/target roles explicit:
  - **source** = desired model
  - **target** = current model to be adjusted
- Prefer `.schema.json` inputs from `ddl-extract` for deterministic comparisons.
- If using `.sql` inputs, keep extraction/parser conventions consistent between both sides.
- Always review generated `.sql` before execution in production.
- Use `.html` report to validate operation order and high-risk changes before applying script.

## Practical interpretation of outputs
- `.sql`: executable adjustment candidate (not auto-applied by tool).
- `.json`: machine-readable list of structured differences.
- `.html`: prioritized visual review with context and suggested sequence.

## Recommended workflow
1. Generate snapshots with `ddl-extract` for both environments.
2. Run `ddl-diff` between source and target snapshots.
3. Review `.html` and `.sql` with DBA/dev.
4. Apply script in staging.
5. Re-run `ddl-diff` to confirm convergence.

## Examples
```powershell
SkyFBTool ddl-diff --source "C:\ddl\source.schema.json" --target "C:\ddl\target.schema.json" --output "C:\ddl\diff"
SkyFBTool ddl-diff --source-ddl "C:\ddl\source.sql" --target-ddl "C:\ddl\target.sql" --output "C:\ddl\diff_from_sql"
SkyFBTool ddl-diff --source "docs\examples\ddl-diff-sample-source.sql" --target "docs\examples\ddl-diff-sample-target.sql" --output "docs\examples\ddl-diff-sample"
```
