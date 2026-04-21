# Documentation Standard

## Language Model
- Primary public docs: English in `README.md` and `CHANGELOG.md`.
- Community mirror: Portuguese (Brazil) in `README.pt-BR.md` and `CHANGELOG.pt-BR.md`.
- Every localized file must include a language switch link at the top.

## File Structure
- `README.md` (English)
- `README.pt-BR.md` (Portuguese)
- `CHANGELOG.md` (English)
- `CHANGELOG.pt-BR.md` (Portuguese)

## Sync Rules
- Keep sections equivalent across languages.
- Update both language files in the same pull request whenever behavior changes.
- Keep commands/flags exactly the same in all language variants.
- Keep the liability disclaimer section mandatory and synchronized in both READMEs.
  - `README.md`: `## Disclaimer`
  - `README.pt-BR.md`: `## Isencao de Responsabilidade`

## Changelog Rules
- Follow Keep a Changelog + Semantic Versioning.
- Keep `## [Unreleased]` always present.
- Allowed sections: `Added`, `Changed`, `Fixed`, `Removed`, `Security`.

## Encoding Rules
- UTF-8 preferred.
- If an editor/environment introduces charset artifacts, normalize file encoding before commit.
