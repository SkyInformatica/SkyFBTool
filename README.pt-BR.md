# SkyFBTool

[English](./README.md) | Português (Brasil)

SkyFBTool é uma CLI em .NET 8 para exportação/importação de dados Firebird (2.5 / 3.0 / 4.0 / 5.0), focada em grandes volumes, execução em streaming e segurança de charset.

## Público-alvo

- DBA: execução operacional, comparação de schema, priorização de risco e validação de rollout.
- Desenvolvedor: artefatos reproduzíveis de schema, revisão de migração, saídas compatíveis com CI e validações automatizadas.

## Guia de escolha de comando

- Precisa mover dados de tabela para script SQL: use `export`.
- Precisa executar script(s) SQL no banco: use `import` (ou `exec-sql` para contexto de manutenção).
- Precisa gerar snapshots de schema (`.sql` + `.schema.json`): use `ddl-extract`.
- Precisa comparar estrutura entre dois schemas: use `ddl-diff`.
- Precisa de relatório de risco/priorização com severidade e sinais operacionais: use `ddl-analyze`.

## O que há de novo

- [CHANGELOG.pt-BR.md](./CHANGELOG.pt-BR.md)
- [Releases](https://github.com/SkyInformatica/SkyFBTool/releases)

## Releases automatizadas

Este repositório possui um pipeline GitHub Actions em `.github/workflows/release.yml`.

Como funciona:
- Disparo: push de tag no formato `v*` (exemplo: `v0.1.0`)
- Pipeline: restore, build, testes, publish (`win-x64` e `linux-x64`)
- Saída: GitHub Release com artefatos compilados (`.tar.gz`)

Exemplo de comando para tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Recursos principais

- Comandos `export`, `import` e `exec-sql`
- Comandos `ddl-extract`, `ddl-diff` e `ddl-analyze` para extração, comparação e análise de risco de schema
- Exportação/importação em streaming para arquivos SQL grandes
- `--filter`, `--filter-file` e modo avançado `--query-file`
- Remapeamento de tabela destino com `--target-table`
- `--blob-format` (`Hex` ou `Base64`)
- `--insert-mode` (`insert` ou `upsert` com `UPDATE OR INSERT ... MATCHING`)
- `--commit-every` e `--progress-every` configuráveis
- Divisão de arquivo com `--split-size-mb` (padrão: 100 MB)
- Modo legado de charset para `CHARSET NONE` com `--legacy-win1252`
- Aviso para arquivos grandes de filtro/consulta (> 64 KB)

## Organização do código

- `Program.cs`: ponto de entrada mínimo
- `Cli/CliApp.cs`: roteamento da CLI + ajuda
- `Cli/Commands/*`: um arquivo por comando (`export`, `import`, `ddl-extract`, `ddl-diff`, `ddl-analyze`)
- `Cli/Common/*`: utilitários compartilhados de parsing de argumentos
- `Services/*`: lógica por contexto (Export, Import, Ddl)
- `Infra/*`: adaptadores técnicos (conexão, encoding, arquivos)

## Uso

```powershell
SkyFBTool export [opções]
SkyFBTool import [opções]
SkyFBTool exec-sql [opções]
SkyFBTool ddl-extract [opções]
SkyFBTool ddl-diff [opções]
SkyFBTool ddl-analyze [opções]
```

## Fluxos recomendados

### 1) Fluxo de migração de dados (DBA/operação)
1. Execute `export` na tabela/consulta de origem.
2. Revise o SQL gerado e os parâmetros de split/charset.
3. Execute `import` no destino monitorando progresso e log.
4. Valide o log de importação e o resumo final.

### 2) Fluxo de promoção de schema (DBA + dev)
1. Rode `ddl-extract` na origem e no destino.
2. Rode `ddl-diff` para gerar comparação SQL/json/html.
3. Revise o HTML e o SQL do diff em homologação.
4. Aplique o SQL aprovado e rode um novo `ddl-diff` para confirmar a convergência.

### 3) Fluxo de triagem de risco (DBA)
1. Execute `ddl-analyze` (preferencialmente em `--database`).
2. Comece pela seção de priorização por tabela no relatório HTML.
3. Trate primeiro itens `critical/high`, depois `medium`.
4. Use itens `low` como backlog de otimização após validação por plano/carga.

### Exemplo de exportação

```powershell
SkyFBTool export --database "C:\dados\exemplo.fdb" --table "TABELA_EXEMPLO" --output "C:\exports\" --commit-every 10000
```

### Exemplo de importação

```powershell
SkyFBTool import --database "C:\dados\exemplo.fdb" --input "C:\exports\tabela_exemplo.sql" --continue-on-error
SkyFBTool import --database "C:\dados\exemplo.fdb" --inputs-batch "C:\exports\*.sql" --continue-on-error
SkyFBTool exec-sql --database "C:\dados\exemplo.fdb" --script "C:\scripts\patch.sql" --continue-on-error
```

### Exemplos de extração e diff de DDL

```powershell
SkyFBTool ddl-extract --database "C:\dados\origem.fdb" --output "C:\ddl\origem"
SkyFBTool ddl-extract --database "C:\dados\alvo.fdb" --output "C:\ddl\alvo"
SkyFBTool ddl-diff --source "C:\ddl\origem.schema.json" --target "C:\ddl\alvo.schema.json" --output "C:\ddl\comparacao"
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --output "C:\ddl\analise"
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$
SkyFBTool ddl-analyze --database "C:\dados\origem.fdb" --output "C:\ddl\analise_do_banco"
SkyFBTool ddl-analyze --databases-batch "C:\dados\*.fdb" --output "C:\ddl\analises_lote\"
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --severity-config ".\docs\examples\ddl-severity.sample.json"
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --description "análise no banco XYZ" --output "C:\ddl\analise_com_contexto"
```

## Observações

- O idioma de saída/relatório de DDL usa detecção da cultura do SO (`English` padrão, `pt-BR` localizado).
- As mensagens de runtime da CLI seguem a cultura do sistema: inglês é a saída padrão e `pt-BR` é usado quando a cultura do SO é português do Brasil.
- `ddl-extract` classifica falhas de extração por categoria raiz (`incompatible_ods`, `permission_denied`, `database_file_access`, `metadata_query_failure`, `connection_failure`, `unknown`).
- Arquivos de saída do `ddl-diff`: `.sql`, `.json` e `.html`.
- O relatório do `ddl-diff` inclui Top 10 achados críticos do alvo (com severidade), ordem sugerida de blocos SQL e checklist pós-aplicação.
- Arquivos de saída do `ddl-analyze`: `.json` e `.html`, com resumo por tipo/tabela e filtros no HTML.
- O relatório HTML do `ddl-analyze` inclui a seção **Tabelas priorizadas para correção** com `Prioridade` (`P0..P3`), `Índice de risco` e `Qtde`, além de legenda de prioridade ao lado dos critérios de severidade.
- No `ddl-analyze --databases-batch`, também é gerado um resumo consolidado: `batch_analysis_summary_*.json` e `.html`.
- `ddl-analyze` suporta dois modos de entrada: arquivo (`--input/--source`) ou conexão direta no banco (`--database` + opções de conexão).
- No `ddl-analyze --database`, o relatório também inclui achados operacionais baseados nas tabelas de monitoramento do Firebird (`MON$`), como sinais de pressão de retenção transacional.
- `ddl-analyze` detecta índices redundantes por prefixo (por exemplo, `(A)` vs `(A,B)`) como achados de otimização.
- `ddl-analyze` suporta modo em lote com `--databases-batch` (`*` e `?`) para analisar vários arquivos `.fdb`.
- `ddl-analyze` aceita `--ignore-table-prefix` (repetível) e `--ignore-table-prefixes` (lista por vírgula) para reduzir ruído de tabelas técnicas.
- `ddl-analyze` aceita `--severity-config` para sobrescrever severidade por código de achado.
- `ddl-analyze` aceita `--description` para incluir texto de contexto nos metadados do JSON e do HTML.
- `ddl-analyze` suporta `--volume-analysis on|off` (padrão `on`) para habilitar/desabilitar a análise de prioridade por volume.
- `ddl-analyze` usa estimativa por índice como padrão e executa `COUNT(*)` exato apenas quando `--volume-count-exact on` é informado explicitamente.
- Use `docs/examples/ddl-severity.sample.json` como referência de formato (cobre todos os códigos atuais).
- Saídas reproduzíveis de exemplo do `ddl-analyze`: `docs/examples/ddl-analyze-sample*.{sql,json,html}`.
- Valores aceitos de severidade: `critical`, `high`, `medium`, `low`.
- O formato do JSON é somente em inglês: `overrides`, `code`, `severity`.

## Principais opções de exportação

- `--database` caminho do banco Firebird
- `--table` tabela de origem
- `--target-table` tabela destino nos `INSERT`s gerados
- `--output` arquivo ou diretório de saída
- `--host` host do Firebird (padrão: `localhost`)
- `--port` porta do Firebird (padrão: `3050`)
- `--user` usuário do Firebird (padrão: `sysdba`)
- `--password` senha do Firebird (padrão: `masterkey`)
- `--charset` `WIN1252 | ISO8859_1 | UTF8 | NONE`
- `--filter` condição simples (opcional)
- `--filter-file` lê condição simples de arquivo
- `--query-file` lê `SELECT` completo de arquivo (modo avançado)
- `--blob-format` `Hex | Base64`
- `--insert-mode` `insert | upsert` (`upsert` exige PK e escreve `MATCHING`)
- `--commit-every` adiciona `COMMIT` a cada N linhas
- `--progress-every` intervalo de progresso
- `--split-size-mb` tamanho da divisão em MB (`0` desativa)
- `--legacy-win1252` modo legado para `CHARSET NONE`
- `--sanitize-text` sanitiza textos antes de escrever o SQL
- `--escape-newlines` escapa quebras de linha em campos texto
- `--continue-on-error` continua exportando se uma linha falhar

Regras:
- Não combinar `--query-file` com `--filter` ou `--filter-file`.
- `--query-file` deve conter um `SELECT` completo.
- `--filter` aceita prefixo `WHERE` (removido automaticamente).

## Principais opções de importação

- `--database` caminho do banco Firebird
- `--input` arquivo SQL de entrada
- `--script` alias explícito de `--input`
- `--inputs-batch` padrão wildcard para múltiplos arquivos SQL de entrada
- `--input-batch` alias de `--inputs-batch`
- `--scripts-batch` alias de `--inputs-batch`
- `--host` host do Firebird (padrão: `localhost`)
- `--port` porta do Firebird (padrão: `3050`)
- `--user` usuário do Firebird (padrão: `sysdba`)
- `--password` senha do Firebird (padrão: `masterkey`)
- `--progress-every` intervalo de progresso
- `--continue-on-error` continua importando após erros de comando
- use apenas um modo de entrada por execução: `--input/--script` ou `--inputs-batch`
- status no resumo do lote:
  - `Sucesso`: arquivo concluído sem erros de comandos SQL
  - `Sucesso com erros`: arquivo concluído com erros de comando usando `--continue-on-error`
  - `Falha`: arquivo interrompido por erro fatal

## Testes

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

### O que os testes garantem hoje

- Exportação:
  - composição segura/válida de `SELECT` (`table`, `columns`, `filter`, `query-file`);
  - consistência na geração de SQL (`INSERT`/`UPSERT`, formatos de BLOB, escape de newline, `commit-every`);
  - cobertura de charset e comportamento legado (`UTF8`, `WIN1252`, `ISO8859_1`, `NONE` + modo legado);
  - exclusão de colunas calculadas/somente leitura e cenários de round-trip export/import.
- Importação / execução SQL:
  - comportamento do parser streaming (`SET TERM`, comentários, literais de string);
  - comportamento fail-fast vs `--continue-on-error` e geração de log de execução;
  - fluxo de entrada em lote, validação de parâmetros e comportamento central de progresso/commit.
- Fluxos DDL:
  - `ddl-extract` gerando snapshot/DDL dos objetos principais;
  - `ddl-diff` detectando mudanças estruturais e sugerindo SQL;
  - `ddl-analyze` com validações estruturais, override de severidade e composição de resumos;
  - checks operacionais (`MON$`) com limites principais e agregação do resumo em lote.
- Infra e CLI:
  - utilitários de detecção/resolução de charset e divisão de arquivo de saída;
  - validação de opções da CLI e classificação contextual de erros.

### Lacunas de cobertura e próximas prioridades

- Resiliência operacional de `MON$` em edge cases de versão/permissão do Firebird.
- Combinações mais profundas de dependências reais no `ddl-diff`, além da ordenação base já implementada.
- Validação de estresse de import/export em execuções longas e datasets de produção (pressão de recurso e estabilidade prolongada).
- Fluxos de lote com resultados mistos (falhas parciais e bases muito heterogêneas).

Testes de integração:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Guia rápido de troubleshooting

- Falha com erros SQL:
  - Consulte o log por execução (`*_import_log_*.log`) e o resumo final.
- Problema de charset/acentuação:
  - Defina `--charset` explícito; use `--legacy-win1252` só em cenário legado `CHARSET NONE` confirmado.
- Execução/log muito grande:
  - Use opções de split/progresso e prefira saída redirecionada em CI.
- `ddl-analyze` sem achados operacionais:
  - Confirme modo por banco (`--database` ou `--databases-batch`) e permissões de leitura em `MON$`.

## Padrão de documentação

- [DOCS_STANDARD.md](./DOCS_STANDARD.md)

## Documentação por comando

- `export`: [docs/commands/pt-BR/export.md](./docs/commands/pt-BR/export.md)
- `import`: [docs/commands/pt-BR/import.md](./docs/commands/pt-BR/import.md)
- `exec-sql`: [docs/commands/pt-BR/exec-sql.md](./docs/commands/pt-BR/exec-sql.md)
- `ddl-extract`: [docs/commands/pt-BR/ddl-extract.md](./docs/commands/pt-BR/ddl-extract.md)
- `ddl-diff`: [docs/commands/pt-BR/ddl-diff.md](./docs/commands/pt-BR/ddl-diff.md)
- `ddl-analyze`: [docs/commands/pt-BR/ddl-analyze.md](./docs/commands/pt-BR/ddl-analyze.md)
- Critérios de severidade/validação do `ddl-analyze`: [docs/commands/pt-BR/ddl-analyze-severity-and-validations.md](./docs/commands/pt-BR/ddl-analyze-severity-and-validations.md)

## Dependências

- `Scriban` é usado para renderizar o HTML do `ddl-analyze` a partir de template.

## Isenção de responsabilidade

O SkyFBTool é distribuído sob a licença MIT, "NO ESTADO EM QUE SE ENCONTRA", sem garantias de qualquer natureza.

Os autores não se responsabilizam por:
- perda de dados
- corrupção de bancos
- falhas de execução
- danos diretos ou indiretos
- uso incorreto
- impactos causados a terceiros

Valide sempre em ambiente de homologação antes do uso em produção.

## Licença

MIT
