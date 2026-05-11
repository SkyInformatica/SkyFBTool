[Português (Brasil)](../pt-BR/testing-and-validation-strategy.md)

# Testing and Validation Strategy

## Purpose

SkyFBTool testing is designed to protect operational predictability, data integrity, and resilient execution under real Firebird workflows.  
The goal is not only code coverage. The goal is to reduce silent regressions in critical database operations.

In practical terms, this document answers: **how far can we operationally trust SkyFBTool behavior**.  
The answer is: there is a strong trust baseline for covered critical flows, with explicit boundaries that remain under human governance.

## Validation Model

The project uses layered validation:

1. Unit and service tests for deterministic behavior and validation rules.
2. CLI and command tests for parameter parsing, aliases, and user-facing contract stability.
3. Integration tests with real Firebird databases for end-to-end execution.
4. Reproducible report artifacts (`docs/examples`) for visual and operational consistency checks.

This combination gives fast regression feedback and realistic operational confidence.

## Test Categories and What They Validate

## 1) Unit and Service-Level Tests

Focus:
- parser and serializer behavior;
- DDL comparison and analysis rules;
- severity normalization, compatibility checks, and risk aggregation.

Operational guarantees:
- deterministic outputs for equal inputs;
- stable rule behavior for DDL findings;
- early detection of logic regressions before integration runs.

## 2) CLI and Command Contract Tests

Focus:
- option parsing and aliases;
- mandatory/invalid argument validation;
- batch wildcard behavior and command routing.

Operational guarantees:
- predictable command-line contract;
- reduced risk of breaking automation scripts and CI pipelines;
- consistent user guidance for operational errors.

## 3) Integration Tests (Real Firebird)

Focus:
- full export/import and DDL flows against live databases;
- real SQL generation and replay;
- charset behavior in UTF8, WIN1252, and legacy-style scenarios.

Operational guarantees:
- end-to-end execution remains functional in realistic environments;
- data movement and replay pipelines preserve expected behavior;
- compatibility confidence for Firebird legacy and modern installations.

## Critical Scenarios Covered Today

Current integration coverage validates:

- export/import roundtrip with real data in UTF8 and WIN1252;
- legacy-related behavior (`NONE` + forced WIN1252) for output compatibility;
- handling of accents and Portuguese text in operational paths;
- resilient behavior under controlled intermittent failures;
- `continue-on-error` workflows with explicit log generation;
- large-volume import/export scenarios with progress and stability expectations;
- `ddl-extract` generation of SQL/snapshot/audit artifacts;
- `ddl-diff` report generation with print-style and visual KPI markers;
- `ddl-analyze` batch summary behavior for bases without findings (`none` / not applicable).

## Operational Guarantees Provided by the Suite

The suite currently provides strong guarantees on:

- **Predictability:** stable command and report behavior for repeatable inputs.
- **Integrity:** data replay paths and schema artifacts remain consistent after changes.
- **Resilience:** failure paths (retry, continue-on-error, logging) remain explicit and testable.
- **Compatibility:** Firebird operational paths are continuously validated across relevant charset/legacy patterns.
- **Regression Control:** critical behavior changes are detected before release publication.

These guarantees represent a preventive engineering model: behavior is validated before production, failure paths are exercised, and operational artifacts are treated as part of the system contract.

## Limits and Required Human Validation

Some areas still require human review, homologation, or controlled production rehearsal:

- performance behavior under production-scale infrastructure variance (I/O, network, storage contention);
- semantic correctness of generated DDL for environment-specific governance policies;
- final human approval for destructive SQL application plans;
- visual readability and print behavior in organization-specific PDF workflows;
- operational decisions for risk acceptance/prioritization in DDL findings.

Testing reduces risk, but does not replace operational governance.

This limit is not a weakness in the process; it is an engineering decision for critical environments: automation validates repeatability and technical safety, while homologation validates business context and residual risk.

## Release Validation Baseline

Before release, the minimum practical baseline is:

1. clean build and automated test execution;
2. integration script execution with real Firebird connectivity;
3. verification of generated artifacts in `docs/examples` when report behavior changes;
4. changelog alignment with shipped behavior.

This keeps releases auditable, reproducible, and operationally trustworthy.

## Operational Trust Level

With the current strategy, SkyFBTool demonstrates:

- engineering maturity in critical data and DDL workflows;
- real operational focus on predictability and resilience;
- active mitigation of silent regressions;
- behavior stability sustained by automated and integration validation.

In short: **the tool is operationally trustworthy within covered scenarios, with clear boundaries for human validation in high-impact decisions**.

## Related Documents

- Operational resilience model: [operational-resilience.md](./operational-resilience.md)
- Risk mitigation model: [risk-mitigation.md](./risk-mitigation.md)
- Schema governance model: [schema-governance.md](./schema-governance.md)
- Reproducible report examples: [../../examples/](../../examples/)
- `ddl-analyze` command guide: [../../commands/en/ddl-analyze.md](../../commands/en/ddl-analyze.md)
- `ddl-diff` command guide: [../../commands/en/ddl-diff.md](../../commands/en/ddl-diff.md)
