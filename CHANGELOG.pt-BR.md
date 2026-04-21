# Registro de Mudancas

[English](./CHANGELOG.md) | Português (Brasil)

Todas as mudancas relevantes deste projeto sao registradas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
e o projeto adota [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Adicionado
- Aviso no console para `--filter-file` e `--query-file` quando o arquivo ultrapassa 64 KB.
- Tratamento mais resiliente de argumentos CLI para casos de PowerShell com `--output` terminando em barra invertida.

### Alterado
- Resumo da exportacao com layout alinhado e mais legivel.
- Mensagem de erro para ausencia de `--table` com orientacao para barra final no PowerShell.

## [0.1.0] - 2026-04-21

### Adicionado
- Comandos principais: `export` e `import`.
- Suporte de exportacao para `--filter`, `--filter-file`, `--query-file` e `--target-table`.
- Divisao automatica de saida com `--split-size-mb`.
- Compatibilidade com Firebird 2.5, 3.0, 4.0 e 5.0 nos fluxos de exportacao/importacao.
- Suite de testes unitarios e de integracao.

[Unreleased]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
