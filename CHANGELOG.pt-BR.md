# Registro de Mudanças

[English](./CHANGELOG.md) | Português (Brasil)

Todas as mudanças relevantes deste projeto são registradas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
e o projeto adota [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Não Lançado]

## [1.0.3] - 2026-07-07

### Corrigido
- `ddl-analyze` não reporta mais procedures emitidas por ferramentas de extração SQL como `ALTER PROCEDURE ... AS` sem bloco PSQL como corpos PSQL ausentes ou inertes.

## [1.0.2] - 2026-07-07

### Adicionado
- Relatórios HTML DDL baseados em Scriban agora expõem uma versão do relatório nos metadados visíveis e nas meta tags HTML para diferenciar futuras mudanças de regras/layout.
- Relatórios HTML DDL baseados em Scriban agora mostram a data/hora local de geração ao lado da data/hora UTC.

### Corrigido
- `ddl-analyze` agora trata comandos `ALTER PROCEDURE`, `ALTER FUNCTION` e `ALTER TRIGGER` em snapshots SQL como a definição final do objeto PSQL, evitando falsos positivos causados por stubs de dependência emitidos antes no script extraído.

## [1.0.1] - 2026-07-05

### Adicionado
- `ddl-analyze` agora reporta procedures, functions e triggers cujo corpo executa apenas `SUSPEND` como objetos PSQL inertes.

### Alterado
- Relatórios HTML do `ddl-analyze` agora incluem uma contagem compacta dos objetos de schema analisados, incluindo tabelas, índices, chaves primárias, chaves estrangeiras, triggers, procedures e functions.
- O relatório HTML do `ddl-analyze` agora identifica a priorização de correção por objeto/escopo em vez de tabela, evitando confusão quando achados se referem a triggers, procedures ou outros objetos.
- `ddl-analyze --databases-batch` agora nomeia cada relatório por banco como `<banco>_schema_analysis_<timestamp>` para facilitar a leitura da saída em lote.

### Corrigido
- `ddl-analyze` não reporta mais procedures extraídas dos metadados do banco como `PROCEDURE_SEM_CORPO` apenas porque o source armazenado contém o corpo PSQL sem a cláusula `AS` externa.

### Removido
- Removida a análise de prioridade por volume do `ddl-analyze`, incluindo `--volume-analysis`, `--volume-count-exact` e achados `OPERACIONAL_VOLUME_PRIORIDADE_*`, para que volume/distribuição de dados seja tratado por um fluxo futuro dedicado.

## [1.0.0] - 2026-07-05

### Adicionado
- `ddl-analyze` agora reporta procedures, functions e triggers sem corpo PSQL válido.
- `ddl-extract` agora preserva procedures, functions e triggers com fonte vazia nos metadados para a análise posterior, emitindo comentários de aviso em vez de SQL inválido para esses objetos.

### Alterado
- `ddl-analyze` agora possui uma estrutura interna de motor de regras, com validações de estrutura de tabela, chaves estrangeiras, índices, compatibilidade de campos e corpo PSQL movidas para classes de regra dedicadas.

### Corrigido
- `ddl-extract` agora filtra metadados de funções Firebird com mais precisão para que UDFs legadas, funções UDR e funções de pacotes não sejam classificadas como funções armazenadas PSQL de nível superior.
- `ddl-analyze` não reporta mais índices por expressão usando concatenação de strings do Firebird (`||`) ou outras expressões que não são colunas como `INDICE_COLUNA_INEXISTENTE`.

## [0.6.3] - 2026-07-04

### Segurança
- `Scriban` atualizado de 7.1.0 para 7.2.5 para remover advisories conhecidos de vulnerabilidade de negação de serviço que afetavam a versão 7.1.0.

### Corrigido
- `ddl-analyze` não reporta mais índices por expressão, como `UPPER(DESCRICAO)`, como `INDICE_COLUNA_INEXISTENTE`.

## [0.6.2] - 2026-05-13

### Adicionado
- Cobertura de integração foi ampliada para fluxos de relatórios DDL:
  - `ddl-analyze` em lote valida severidade máxima `none` para bases sem achados;
  - `ddl-diff` valida geração de HTML com estilo de impressão e marcadores visuais de indicadores.
- Novo comando `create-db` para provisionar arquivos de banco Firebird com opções operacionais explícitas (`charset`, `page-size`, `forced-writes`) e comportamento seguro de sobrescrita.
- `create-db` agora suporta `--ddl-file` para inicializar o schema logo após a criação do banco, executando um script SQL extraído.

### Alterado
- Mensagem de console em PT-BR do `ddl-analyze --databases-batch` foi refinada para texto mais claro (`Padrão de bancos correspondeu a ... arquivo(s)`).

### Corrigido
- `create-db` agora valida a existência de `--ddl-file` antes de tentar criação/conexão do banco, garantindo comportamento determinístico de `FileNotFoundException` nos testes da CLI em diferentes ambientes.
- O pipeline `ddl-extract`/`create-db --ddl-file` agora trata cenários críticos de compatibilidade na inicialização de schema:
  - ordenação determinística de PK/UNIQUE/FK para evitar falhas de dependência de metadados;
  - geração da sintaxe correta de índice descendente no Firebird (`CREATE DESCENDING INDEX`);
  - normalização de default de parâmetros de procedure para assinaturas PSQL válidas;
  - ordenação de rotinas orientada a dependência, incluindo padrões de uso via `FROM` e `JOIN`;
  - suporte a referências circulares entre rotinas com emissão em duas fases (stub + corpo completo);
  - correção do mapeamento de tipos modernos do Firebird (`DOUBLE PRECISION`, `TIME WITH TIME ZONE`, `TIMESTAMP WITH TIME ZONE`);
  - extração e geração de objetos `EXCEPTION` customizados exigidos por procedures.
- `create-db` agora propaga consistentemente o locale detectado da CLI para a importação do DDL, evitando mistura de mensagens PT-BR/EN na mesma execução.

## [0.5.0] - 2026-05-10

### Adicionado
- Importação e exportação agora aplicam política de retry automático para falhas transitórias (até 3 tentativas) em cenários de instabilidade de execução e escrita.
- `ddl-diff` agora suporta `--include-domains` para comparar objetos `DOMAIN` de forma opcional, mantendo a ignorância por padrão para revisões mais práticas.

### Alterado
- A análise operacional de `ddl-analyze --database` foi reforçada com classificação explícita de status/erro da coleta MON$ (sucesso, parcial ou falha com contexto nos metadados do relatório).
- Layout e elementos visuais de severidade/prioridade no relatório `ddl-analyze` foram refinados para maior consistência entre relatório rico e relatório em lote.
- Renderização HTML de `ddl-analyze` agora remove campos internos de filtro de risco não utilizados (`ScoreRisco`, `Prioridade`) do modelo/payload de saída.
- `export` e `import` agora compartilham padrão de progresso no console: linha dinâmica em terminal interativo, checkpoints fixos periódicos (50 mil unidades ou 30s) e fallback em linhas fixas para saída redirecionada/CI.
- `ddl-diff` agora gera SQL em ordem determinística orientada a dependências (remoção de constraints, criação/alteração de estrutura, PK, índices e por último FK), reduzindo falhas por dependência na aplicação.
- `ddl-diff` agora ignora diferenças de `DOMAIN` por padrão e só as inclui quando `--include-domains` é habilitado explicitamente.
- Documentação dos comandos (EN/PT-BR) e README foram atualizados para refletir a ordenação por dependência e a política de retry transitório.
- Artefatos de exemplo DDL e documentação de comandos foram atualizados para refletir o comportamento visual/funcional atual.
- README (EN/PT-BR) foi reestruturado como portal estratégico de documentação, e os documentos conceituais foram organizados em `docs/concepts/en` e `docs/concepts/pt-BR` com navegação bilíngue.
- O resumo da análise DDL em lote agora usa `Não aplicável` para bases sem achados, evitando senso falso de urgência na maior severidade.

### Corrigido
- Resumo do `import` em lote agora classifica corretamente arquivos com erros de comandos SQL em `--continue-on-error` como `Sucesso com erros`, em vez de `Sucesso`.
- Títulos dos relatórios DDL agora preservam corretamente acentuação UTF-8 nos fluxos de impressão/geração de PDF (exemplo: `Análise de Risco DDL`).
- Tratamento silencioso de exceção na coleta da data de manutenção foi substituído por tratamento resiliente explícito para manter diagnóstico consistente.

## [0.4.0] - 2026-04-29

### Adicionado
- `ddl-analyze` agora suporta `--volume-analysis on|off` (padrão: `on`) para habilitar/desabilitar explicitamente a análise de prioridade por volume via SQL.
- `ddl-analyze` agora suporta `--volume-count-exact on|off` (padrão: `off`) para opcionalmente executar `COUNT(*)` exato por tabela na análise de volume.
- Metadados do relatório em `ddl-analyze --database` agora incluem data estimada da última manutenção via `MON$DATABASE.MON$CREATION_DATE` (criação/último restore do banco).

### Alterado
- Achados `FK_SEM_INDICE_COBERTURA` agora incluem contexto mais completo no relatório (tabela/colunas filha e tabela/colunas pai).
- Achados `INDICE_DUPLICADO` agora incluem a assinatura de índice calculada para facilitar validação de duplicidade por DBAs.
- `ddl-analyze` agora emite achados operacionais de prioridade por volume (`OPERACIONAL_VOLUME_PRIORIDADE_ALTA|MEDIA|BAIXA`) usando estimativa leve por índice no modo por banco.
- Relatório HTML de `ddl-analyze` agora inclui seção de priorização para correção por tabela (`Tabelas priorizadas para correção`) com `Prioridade` (`P0..P3`), `Índice de risco` e `Qtde`.
- Layout do relatório `ddl-analyze` agora exibe a legenda de prioridade (`P0..P3`) ao lado dos critérios de severidade e alinha os painéis de resumo com áreas de rolagem de altura fixa.
- Pipeline/versionamento de release foi alinhado para derivar versão de build a partir da tag Git (`v*`).
- Documentação do `ddl-analyze` e relatórios de exemplo foram atualizados.

### Corrigido
- Falsos positivos de `FK_SEM_INDICE_COBERTURA` foram corrigidos ao considerar metadados do índice de suporte da FK (índice vinculado à constraint) tanto na extração por banco quanto na análise por snapshot SQL.

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

[Não Lançado]: https://github.com/SkyInformatica/SkyFBTool/compare/v1.0.3...HEAD
[1.0.3]: https://github.com/SkyInformatica/SkyFBTool/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/SkyInformatica/SkyFBTool/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/SkyInformatica/SkyFBTool/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.6.3...v1.0.0
[0.6.3]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.6.2...v0.6.3
[0.6.2]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.5.0...v0.6.2
[0.5.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/SkyInformatica/SkyFBTool/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/SkyInformatica/SkyFBTool/releases/tag/v0.1.0
