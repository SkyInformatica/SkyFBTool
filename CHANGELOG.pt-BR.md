# Registro de Mudanças

[English](./CHANGELOG.md) | Português (Brasil)

Todas as mudanças relevantes deste projeto são registradas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
e o projeto adota [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Não Lançado]

## [0.3.0] - 2026-04-28

### Adicionado
- `ddl-analyze` agora suporta entrada direta por banco (`--database` + opções de conexão), extraindo metadados internamente antes da análise.
- `ddl-analyze` agora suporta modo em lote via `--databases-batch` (`*`, `?`) para executar análise sobre múltiplos arquivos `.fdb`.
- `ddl-analyze --database` agora inclui achados operacionais das tabelas de monitoramento do Firebird (`MON$`) no mesmo relatório de risco.
- `import` agora suporta modo em lote para executar múltiplos arquivos SQL por padrão wildcard (`--inputs-batch`, aliases: `--input-batch`, `--scripts-batch`).
- Importação agora sempre gera arquivo de log por execução com nome único (`*_import_log_*.log`), incluindo status explícito de conclusão com/sem erros.
- `ddl-extract` agora classifica falhas de extração por categoria raiz (`incompatible_ods`, `permission_denied`, `database_file_access`, `metadata_query_failure`, `connection_failure`, `unknown`).
- `ddl-analyze` agora detecta índice redundante por prefixo (`REDUNDANT_PREFIX_INDEX`) como achado de otimização.
- Novos arquivos reproduzíveis de exemplo do `ddl-analyze` foram adicionados em `docs/examples` (`sample` e `sample-rich` em `.sql/.json/.html`).
- `ddl-analyze` agora suporta `--description` para incluir texto de contexto nos metadados do relatório (`Description`) tanto no JSON quanto no HTML.

### Alterado
- Ajuda e documentação de comando do `ddl-analyze` atualizadas para descrever o modo por arquivo e o modo por conexão direta.
- Documentação dos comandos foi completada para listar explicitamente todos os parâmetros e aliases suportados (EN/PT-BR).
- Documentação do relatório `ddl-analyze` agora aponta para exemplos HTML vivos em vez de screenshots estáticos.
- Estilos de impressão do HTML do `ddl-analyze` foram melhorados para exportação em PDF (layout A4, quebra de linhas em tabelas e páginas mais limpa).
- PNGs obsoletos de screenshot foram removidos de `docs/examples` e os mapeamentos legados na solução foram limpos.

### Corrigido
- Comportamento padrão do `import` para `--continue-on-error` foi corrigido: sem a flag, a importação agora para no primeiro erro de execução SQL.

## [0.2.0] - 2026-04-25

### Adicionado
- Aviso no console para `--filter-file` e `--query-file` quando o arquivo ultrapassa 64 KB.
- Tratamento mais resiliente de argumentos CLI para casos de PowerShell com `--output` terminando em barra invertida.
- Novo comando `ddl-extract` para extrair schema normalizado (`.sql` + `.schema.json`).
- Novo comando `ddl-diff` para comparar origem/alvo e gerar relatório (`.sql`, `.json` e `.md`).
- `ddl-diff` agora também gera relatório visual em `.html`.
- `ddl-analyze` agora aceita `.sql` diretamente (com fallback para parser interno de DDL quando `.schema.json` não estiver presente).
- Novo suporte de sobrescrita de severidade no `ddl-analyze` via `--severity-config` (`overrides`, `code`, `severity`).
- Nova documentação por comando em `docs/commands/en` e `docs/commands/pt-BR`.

### Alterado
- Resumo da exportação com layout alinhado e mais legível.
- Mensagem de erro para ausência de `--table` com orientação para barra final no PowerShell.
- CLI reorganizada por contexto (`Cli/Commands` e `Cli/Common`) com `Program.cs` mínimo.
- Saída dos comandos/relatórios de DDL agora detecta cultura do SO (`en` padrão, `pt-BR` localizado).
- Geração HTML de `ddl-diff` extraída para renderizador/template dedicado (baseado em Scriban).
- Validação de FKs na análise DDL quebrada em funções menores por tipo de validação.
- Validação de opções CLI desconhecidas padronizada entre os comandos.
- Relatório de `ddl-analyze` agora inclui critérios explícitos de severidade no HTML.
- Exemplo de configuração de severidade padronizado com aliases em inglês em `docs/examples/ddl-severity.sample.json`.

### Corrigido
- Mensagens em português do relatório DDL (`descrição`/`recomendação`) normalizadas com acentuação correta.

## [0.1.0] - 2026-04-21

### Adicionado
- Comandos principais: `export` e `import`.
- Suporte de exportação para `--filter`, `--filter-file`, `--query-file` e `--target-table`.
- Divisão automática de saída com `--split-size-mb`.
- Compatibilidade com Firebird 2.5, 3.0, 4.0 e 5.0 nos fluxos de exportação/importação.
- Suíte de testes unitários e de integração.

[Não Lançado]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
