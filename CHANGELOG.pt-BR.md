# Registro de MudanÃ§as

[English](./CHANGELOG.md) | PortuguÃªs (Brasil)

Todas as mudanÃ§as relevantes deste projeto sÃ£o registradas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
e o projeto adota [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [NÃ£o LanÃ§ado]

### Adicionado
- ImportaÃ§Ã£o e exportaÃ§Ã£o agora aplicam polÃ­tica de retry automÃ¡tico para falhas transitÃ³rias (atÃ© 3 tentativas) em cenÃ¡rios de instabilidade de execuÃ§Ã£o e escrita.
- `ddl-diff` agora suporta `--include-domains` para comparar objetos `DOMAIN` de forma opcional, mantendo a ignorÃ¢ncia por padrÃ£o para revisÃµes mais prÃ¡ticas.

### Alterado
- A anÃ¡lise operacional de `ddl-analyze --database` foi reforÃ§ada com classificaÃ§Ã£o explÃ­cita de status/erro da coleta MON$ (sucesso, parcial ou falha com contexto nos metadados do relatÃ³rio).
- Layout e elementos visuais de severidade/prioridade no relatÃ³rio `ddl-analyze` foram refinados para maior consistÃªncia entre relatÃ³rio rico e relatÃ³rio em lote.
- RenderizaÃ§Ã£o HTML de `ddl-analyze` agora remove campos internos de filtro de risco nÃ£o utilizados (`ScoreRisco`, `Prioridade`) do modelo/payload de saÃ­da.
- `export` e `import` agora compartilham padrÃ£o de progresso no console: linha dinÃ¢mica em terminal interativo, checkpoints fixos periÃ³dicos (50 mil unidades ou 30s) e fallback em linhas fixas para saÃ­da redirecionada/CI.
- `ddl-diff` agora gera SQL em ordem determinÃ­stica orientada a dependÃªncias (remoÃ§Ã£o de constraints, criaÃ§Ã£o/alteraÃ§Ã£o de estrutura, PK, Ã­ndices e por Ãºltimo FK), reduzindo falhas por dependÃªncia na aplicaÃ§Ã£o.
- `ddl-diff` agora ignora diferenÃ§as de `DOMAIN` por padrÃ£o e sÃ³ as inclui quando `--include-domains` Ã© habilitado explicitamente.
- DocumentaÃ§Ã£o dos comandos (EN/PT-BR) e README foram atualizados para refletir a ordenaÃ§Ã£o por dependÃªncia e a polÃ­tica de retry transitÃ³rio.
- Artefatos de exemplo DDL e documentaÃ§Ã£o de comandos foram atualizados para refletir o comportamento visual/funcional atual.
- README (EN/PT-BR) foi reestruturado como portal estratégico de documentação, e os documentos conceituais foram organizados em `docs/concepts/en` e `docs/concepts/pt-BR` com navegação bilíngue.
- O resumo da análise DDL em lote agora usa `Não aplicável` para bases sem achados, evitando senso falso de urgência na maior severidade.

### Corrigido
- Resumo do `import` em lote agora classifica corretamente arquivos com erros de comandos SQL em `--continue-on-error` como `Sucesso com erros`, em vez de `Sucesso`.
- Títulos dos relatórios DDL agora preservam corretamente acentuação UTF-8 nos fluxos de impressão/geração de PDF (exemplo: `Análise de Risco DDL`).
- Tratamento silencioso de exceção na coleta da data de manutenção foi substituído por tratamento resiliente explícito para manter diagnóstico consistente.

## [0.4.0] - 2026-04-29

### Adicionado
- `ddl-analyze` agora suporta `--volume-analysis on|off` (padrÃ£o: `on`) para habilitar/desabilitar explicitamente a anÃ¡lise de prioridade por volume via SQL.
- `ddl-analyze` agora suporta `--volume-count-exact on|off` (padrÃ£o: `off`) para opcionalmente executar `COUNT(*)` exato por tabela na anÃ¡lise de volume.
- Metadados do relatÃ³rio em `ddl-analyze --database` agora incluem data estimada da Ãºltima manutenÃ§Ã£o via `MON$DATABASE.MON$CREATION_DATE` (criaÃ§Ã£o/Ãºltimo restore do banco).

### Alterado
- Achados `FK_SEM_INDICE_COBERTURA` agora incluem contexto mais completo no relatÃ³rio (tabela/colunas filha e tabela/colunas pai).
- Achados `INDICE_DUPLICADO` agora incluem a assinatura de Ã­ndice calculada para facilitar validaÃ§Ã£o de duplicidade por DBAs.
- `ddl-analyze` agora emite achados operacionais de prioridade por volume (`OPERACIONAL_VOLUME_PRIORIDADE_ALTA|MEDIA|BAIXA`) usando estimativa leve por Ã­ndice no modo por banco.
- RelatÃ³rio HTML de `ddl-analyze` agora inclui seÃ§Ã£o de priorizaÃ§Ã£o para correÃ§Ã£o por tabela (`Tabelas priorizadas para correÃ§Ã£o`) com `Prioridade` (`P0..P3`), `Ãndice de risco` e `Qtde`.
- Layout do relatÃ³rio `ddl-analyze` agora exibe a legenda de prioridade (`P0..P3`) ao lado dos critÃ©rios de severidade e alinha os painÃ©is de resumo com Ã¡reas de rolagem de altura fixa.
- Pipeline/versionamento de release foi alinhado para derivar versÃ£o de build a partir da tag Git (`v*`).
- DocumentaÃ§Ã£o do `ddl-analyze` e relatÃ³rios de exemplo foram atualizados.

### Corrigido
- Falsos positivos de `FK_SEM_INDICE_COBERTURA` foram corrigidos ao considerar metadados do Ã­ndice de suporte da FK (Ã­ndice vinculado Ã  constraint) tanto na extraÃ§Ã£o por banco quanto na anÃ¡lise por snapshot SQL.

## [0.3.0] - 2026-04-28

### Adicionado
- `ddl-analyze` agora suporta entrada direta por banco (`--database` + opÃ§Ãµes de conexÃ£o), extraindo metadados internamente antes da anÃ¡lise.
- `ddl-analyze` agora suporta modo em lote via `--databases-batch` (`*`, `?`) para executar anÃ¡lise sobre mÃºltiplos arquivos `.fdb`.
- `ddl-analyze --database` agora inclui achados operacionais das tabelas de monitoramento do Firebird (`MON$`) no mesmo relatÃ³rio de risco.
- `import` agora suporta modo em lote para executar mÃºltiplos arquivos SQL por padrÃ£o wildcard (`--inputs-batch`, aliases: `--input-batch`, `--scripts-batch`).
- ImportaÃ§Ã£o agora sempre gera arquivo de log por execuÃ§Ã£o com nome Ãºnico (`*_import_log_*.log`), incluindo status explÃ­cito de conclusÃ£o com/sem erros.
- `ddl-extract` agora classifica falhas de extraÃ§Ã£o por categoria raiz (`incompatible_ods`, `permission_denied`, `database_file_access`, `metadata_query_failure`, `connection_failure`, `unknown`).
- `ddl-analyze` agora detecta Ã­ndice redundante por prefixo (`REDUNDANT_PREFIX_INDEX`) como achado de otimizaÃ§Ã£o.
- Novos arquivos reproduzÃ­veis de exemplo do `ddl-analyze` foram adicionados em `docs/examples` (`sample` e `sample-rich` em `.sql/.json/.html`).
- `ddl-analyze` agora suporta `--description` para incluir texto de contexto nos metadados do relatÃ³rio (`Description`) tanto no JSON quanto no HTML.

### Alterado
- Ajuda e documentaÃ§Ã£o de comando do `ddl-analyze` atualizadas para descrever o modo por arquivo e o modo por conexÃ£o direta.
- DocumentaÃ§Ã£o dos comandos foi completada para listar explicitamente todos os parÃ¢metros e aliases suportados (EN/PT-BR).
- DocumentaÃ§Ã£o do relatÃ³rio `ddl-analyze` agora aponta para exemplos HTML vivos em vez de screenshots estÃ¡ticos.
- Estilos de impressÃ£o do HTML do `ddl-analyze` foram melhorados para exportaÃ§Ã£o em PDF (layout A4, quebra de linhas em tabelas e pÃ¡ginas mais limpa).
- PNGs obsoletos de screenshot foram removidos de `docs/examples` e os mapeamentos legados na soluÃ§Ã£o foram limpos.

### Corrigido
- Comportamento padrÃ£o do `import` para `--continue-on-error` foi corrigido: sem a flag, a importaÃ§Ã£o agora para no primeiro erro de execuÃ§Ã£o SQL.

## [0.2.0] - 2026-04-25

### Adicionado
- Aviso no console para `--filter-file` e `--query-file` quando o arquivo ultrapassa 64 KB.
- Tratamento mais resiliente de argumentos CLI para casos de PowerShell com `--output` terminando em barra invertida.
- Novo comando `ddl-extract` para extrair schema normalizado (`.sql` + `.schema.json`).
- Novo comando `ddl-diff` para comparar origem/alvo e gerar relatÃ³rio (`.sql`, `.json` e `.md`).
- `ddl-diff` agora tambÃ©m gera relatÃ³rio visual em `.html`.
- `ddl-analyze` agora aceita `.sql` diretamente (com fallback para parser interno de DDL quando `.schema.json` nÃ£o estiver presente).
- Novo suporte de sobrescrita de severidade no `ddl-analyze` via `--severity-config` (`overrides`, `code`, `severity`).
- Nova documentaÃ§Ã£o por comando em `docs/commands/en` e `docs/commands/pt-BR`.

### Alterado
- Resumo da exportaÃ§Ã£o com layout alinhado e mais legÃ­vel.
- Mensagem de erro para ausÃªncia de `--table` com orientaÃ§Ã£o para barra final no PowerShell.
- CLI reorganizada por contexto (`Cli/Commands` e `Cli/Common`) com `Program.cs` mÃ­nimo.
- SaÃ­da dos comandos/relatÃ³rios de DDL agora detecta cultura do SO (`en` padrÃ£o, `pt-BR` localizado).
- GeraÃ§Ã£o HTML de `ddl-diff` extraÃ­da para renderizador/template dedicado (baseado em Scriban).
- ValidaÃ§Ã£o de FKs na anÃ¡lise DDL quebrada em funÃ§Ãµes menores por tipo de validaÃ§Ã£o.
- ValidaÃ§Ã£o de opÃ§Ãµes CLI desconhecidas padronizada entre os comandos.
- RelatÃ³rio de `ddl-analyze` agora inclui critÃ©rios explÃ­citos de severidade no HTML.
- Exemplo de configuraÃ§Ã£o de severidade padronizado com aliases em inglÃªs em `docs/examples/ddl-severity.sample.json`.

### Corrigido
- Mensagens em portuguÃªs do relatÃ³rio DDL (`descriÃ§Ã£o`/`recomendaÃ§Ã£o`) normalizadas com acentuaÃ§Ã£o correta.

## [0.1.0] - 2026-04-21

### Adicionado
- Comandos principais: `export` e `import`.
- Suporte de exportaÃ§Ã£o para `--filter`, `--filter-file`, `--query-file` e `--target-table`.
- DivisÃ£o automÃ¡tica de saÃ­da com `--split-size-mb`.
- Compatibilidade com Firebird 2.5, 3.0, 4.0 e 5.0 nos fluxos de exportaÃ§Ã£o/importaÃ§Ã£o.
- SuÃ­te de testes unitÃ¡rios e de integraÃ§Ã£o.

[NÃ£o LanÃ§ado]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
