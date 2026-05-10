English | [Português (Brasil)](../pt-BR/firebird-compatibility.md)

# Firebird Compatibility Model

SkyFBTool targets operational compatibility across Firebird `2.5`, `3.0`, `4.0`, and `5.0`.

## Compatibility Priorities

- preserve behavior in mixed-version environments;
- keep workflows viable for legacy databases;
- make charset handling explicit and reviewable;
- expose version-sensitive findings in analysis outputs.

## Charset and Encoding Considerations

- export includes `SET NAMES`;
- import detects and respects `SET NAMES`;
- legacy `CHARSET NONE` scenarios are supported via `--legacy-win1252`;
- explicit charset settings are recommended in critical migrations.

## Legacy-Safe Operation Notes

- validate encoding assumptions before large import/export runs;
- treat legacy mode as controlled exception, not default baseline;
- prefer reproducible scripts and staged verification for old databases.

## Version-Aware Structural Validation

`ddl-analyze` includes compatibility-oriented checks (for example, type/precision/version constraints) to prevent unsupported DDL promotion.

## Practical Guidance

1. Define target Firebird version baseline per environment.
2. Extract and analyze schema before promotion.
3. Resolve incompatible findings before rollout.
4. Re-run checks after apply to confirm stability.
