# Documentation Standard

## Language Model
- Primary public docs: English in `README.md` and `CHANGELOG.md`.
- Community mirror: Portuguese (Brazil) in `README.pt-BR.md` and `CHANGELOG.pt-BR.md`.
- Every localized file must include a language switch link at the top.
- Runtime/report output default language: English.
- Optional localized runtime/report output can be enabled by OS culture detection (for example, PT-BR).

## File Structure
- `README.md` (English)
- `README.pt-BR.md` (Portuguese)
- `CHANGELOG.md` (English)
- `CHANGELOG.pt-BR.md` (Portuguese)

## Documentation Organization
- Documentation must live under `docs/` (except top-level project files like README/CHANGELOG).
- Prefer language folders for mirrored content (for example, `docs/<area>/en/` and `docs/<area>/pt-BR/`).
- New conceptual, operational, or reference docs must be created in both English and Portuguese variants.
- Keep localized pairs synchronized and linked with language switch links at the top.
- Whenever a new document is added, it must be organized in the appropriate `docs/...` structure and added to the `*.sln` as Solution Items/Solution Folders for discoverability in the IDE.

## Sync Rules
- Keep sections equivalent across languages.
- Update both language files in the same pull request whenever behavior changes.
- Keep commands/flags exactly the same in all language variants.
- Review Portuguese mirror files for correct spelling and accentuation before commit.
- Keep the liability disclaimer section mandatory and synchronized in both READMEs.
  - `README.md`: `## Disclaimer`
  - `README.pt-BR.md`: `## Isencao de Responsabilidade`
- When adding report messages, write English strings first and keep a mapped PT-BR variant for localization.

## Changelog Rules
- Follow Keep a Changelog + Semantic Versioning.
- Keep pending-version section always present:
  - English: `## [Unreleased]`
  - Portuguese: `## [Não Lançado]`
- Allowed sections: `Added`, `Changed`, `Fixed`, `Removed`, `Security`.

## Encoding Rules
- UTF-8 preferred.
- If an editor/environment introduces charset artifacts, normalize file encoding before commit.
