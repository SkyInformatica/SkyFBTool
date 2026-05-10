English | [Português (Brasil)](../pt-BR/risk-mitigation.md)

# Risk Mitigation Model

SkyFBTool treats Firebird operations as risk-bearing engineering workflows, not only command execution.

## Goals

- reduce probability of operational incidents;
- reduce blast radius when incidents occur;
- improve predictability before rollout;
- improve traceability for post-incident analysis.

## Core Mitigation Layers

1. Structural analysis  
   `ddl-analyze` evaluates schema findings by severity and risk context.

2. Operational signals  
   In DB mode, `MON$` data adds live pressure indicators to structural analysis.

3. Prioritization  
   Findings are ordered by severity/risk index to focus remediation on highest impact first.

4. Controlled execution  
   Streaming, commit pacing, and resilient execution options reduce runtime risk.

5. Auditability  
   JSON/HTML artifacts and logs support review, change approval, and postmortem workflows.

## Typical Risk-First Sequence

1. Extract baseline (`ddl-extract`).
2. Compare environments (`ddl-diff`).
3. Analyze risk (`ddl-analyze`).
4. Remediate with staged SQL execution.
5. Re-run analysis to confirm risk reduction.

## Scope Notes

- File mode focuses on structural risk.
- DB mode combines structural and operational risk.
- Severity overrides can adapt policy, but should be version-controlled and reviewed.
