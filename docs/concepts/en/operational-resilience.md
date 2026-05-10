English | [Português (Brasil)](../pt-BR/operational-resilience.md)

# Operational Resilience Model

SkyFBTool is designed for long-running and high-volume Firebird operations with resilience-oriented execution patterns.

## Resilience Objectives

- continue safely under partial failures when policy allows;
- avoid memory spikes in large data/script processing;
- preserve progress visibility and diagnosability;
- keep execution behavior predictable under load.

## Mechanisms

1. Streaming execution  
   Large files and result sets are processed incrementally.

2. Retry-aware behavior  
   Transient failure handling reduces unnecessary aborts.

3. Continue-on-error controls  
   `--continue-on-error` supports continuity for operational batch scenarios.

4. Commit and progress pacing  
   Configurable commit/progress intervals improve operational control.

5. Output partitioning  
   `--split-size-mb` allows manageable export artifacts.

6. Operational logging  
   Per-run logs support diagnosis and accountability.

## Recommended Practice

- default to fail-fast when data integrity is priority;
- enable continue-on-error only with explicit operational policy;
- monitor log growth and disk usage in large runs;
- keep reproducible command lines and outputs for post-run audit.
