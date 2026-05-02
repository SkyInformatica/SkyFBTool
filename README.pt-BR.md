# SkyFBTool

[English](./README.md) | PortuguÃªs (Brasil)

SkyFBTool Ã© uma CLI em .NET 8 para exportaÃ§Ã£o/importaÃ§Ã£o de dados Firebird (2.5 / 3.0 / 4.0 / 5.0), focada em grandes volumes, execuÃ§Ã£o em streaming e seguranÃ§a de charset.

## PÃºblico-alvo

- DBA: execuÃ§Ã£o operacional, comparaÃ§Ã£o de schema, priorizaÃ§Ã£o de risco e validaÃ§Ã£o de rollout.
- Desenvolvedor: artefatos reprodutÃ­veis de schema, revisÃ£o de migraÃ§Ã£o, saÃ­das compatÃ­veis com CI e validaÃ§Ãµes automatizadas.

## Guia de escolha de comando

- Precisa mover dados de tabela para script SQL: use `export`.
- Precisa executar script(s) SQL no banco: use `import` (ou `exec-sql` para contexto de manutenÃ§Ã£o).
- Precisa gerar snapshots de schema (`.sql` + `.schema.json`): use `ddl-extract`.
- Precisa comparar estrutura entre dois schemas: use `ddl-diff`.
- Precisa de relatÃ³rio de risco/priorizaÃ§Ã£o com severidade e sinais operacionais: use `ddl-analyze`.

## O Que HÃ¡ de Novo

- [CHANGELOG.pt-BR.md](./CHANGELOG.pt-BR.md)
- [Releases](https://github.com/SkyInformatica/SkyFBTool/releases)

## Releases Automatizadas

Este repositÃ³rio possui um pipeline GitHub Actions em `.github/workflows/release.yml`.

Como funciona:
- Disparo: push de tag no formato `v*` (exemplo: `v0.1.0`)
- Pipeline: restore, build, testes, publish (`win-x64` e `linux-x64`)
- SaÃ­da: GitHub Release com artefatos compilados (`.tar.gz`)

Exemplo de comando para tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Recursos Principais

- Comandos `export`, `import` e `exec-sql`
- Comandos `ddl-extract`, `ddl-diff` e `ddl-analyze` para extraÃ§Ã£o, comparaÃ§Ã£o e anÃ¡lise de risco de schema
- ExportaÃ§Ã£o/importaÃ§Ã£o em streaming para arquivos SQL grandes
- `--filter`, `--filter-file` e modo avanÃ§ado `--query-file`
- Remapeamento de tabela destino com `--target-table`
- `--blob-format` (`Hex` ou `Base64`)
- `--insert-mode` (`insert` ou `upsert` com `UPDATE OR INSERT ... MATCHING`)
- `--commit-every` e `--progress-every` configurÃ¡veis
- DivisÃ£o de arquivo com `--split-size-mb` (padrÃ£o: 100 MB)
- Modo legado de charset para `CHARSET NONE` com `--legacy-win1252`
- Aviso para arquivos grandes de filtro/consulta (> 64 KB)

## OrganizaÃ§Ã£o do CÃ³digo

- `Program.cs`: ponto de entrada mÃ­nimo
- `Cli/CliApp.cs`: roteamento da CLI + ajuda
- `Cli/Commands/*`: um arquivo por comando (`export`, `import`, `ddl-extract`, `ddl-diff`, `ddl-analyze`)
- `Cli/Common/*`: utilitÃ¡rios compartilhados de parsing de argumentos
- `Services/*`: lÃ³gica por contexto (Export, Import, Ddl)
- `Infra/*`: adaptadores tÃ©cnicos (conexÃ£o, encoding, arquivos)

## Uso

```powershell
SkyFBTool export [opÃ§Ãµes]
SkyFBTool import [opÃ§Ãµes]
SkyFBTool exec-sql [opÃ§Ãµes]
SkyFBTool ddl-extract [opÃ§Ãµes]
SkyFBTool ddl-diff [opÃ§Ãµes]
SkyFBTool ddl-analyze [opÃ§Ãµes]
```

## Fluxos recomendados

### 1) Fluxo de migraÃ§Ã£o de dados (DBA/operaÃ§Ã£o)
1. Execute `export` na tabela/consulta de origem.
2. Revise SQL gerado e parÃ¢metros de split/charset.
3. Execute `import` no destino monitorando progresso e log.
4. Valide log de importaÃ§Ã£o e resumo final.

### 2) Fluxo de promoÃ§Ã£o de schema (DBA + dev)
1. Rode `ddl-extract` em origem e destino.
2. Rode `ddl-diff` para gerar comparaÃ§Ã£o SQL/json/html.
3. Revise HTML e SQL do diff em homologaÃ§Ã£o.
4. Aplique SQL aprovado e rode novo `ddl-diff` para confirmar convergÃªncia.

### 3) Fluxo de triagem de risco (DBA)
1. Execute `ddl-analyze` (preferencialmente em `--database`).
2. Comece pela seÃ§Ã£o de priorizaÃ§Ã£o por tabela no relatÃ³rio HTML.
3. Trate primeiro itens `critical/high`, depois `medium`.
4. Use itens `low` como backlog de otimizaÃ§Ã£o apÃ³s validaÃ§Ã£o por plano/carga.

### Exemplo de exportaÃ§Ã£o

```powershell
SkyFBTool export --database "C:\dados\exemplo.fdb" --table "TABELA_EXEMPLO" --output "C:\exports\" --commit-every 10000
```

### Exemplo de importaÃ§Ã£o

```powershell
SkyFBTool import --database "C:\dados\exemplo.fdb" --input "C:\exports\tabela_exemplo.sql" --continue-on-error
SkyFBTool import --database "C:\dados\exemplo.fdb" --inputs-batch "C:\exports\*.sql" --continue-on-error
SkyFBTool exec-sql --database "C:\dados\exemplo.fdb" --script "C:\scripts\patch.sql" --continue-on-error
```

### Exemplos de extraÃ§Ã£o e diff de DDL

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

ObservaÃ§Ãµes:
- O idioma de saÃ­da/relatÃ³rio de DDL usa detecÃ§Ã£o da cultura do SO (`English` padrÃ£o, `pt-BR` localizado).
- `ddl-extract` classifica falhas de extraÃ§Ã£o por categoria raiz (`incompatible_ods`, `permission_denied`, `database_file_access`, `metadata_query_failure`, `connection_failure`, `unknown`).
- Arquivos de saÃ­da do `ddl-diff`: `.sql`, `.json` e `.html`.
- O relatÃ³rio do `ddl-diff` inclui Top 10 achados crÃ­ticos do alvo (com severidade), ordem sugerida de blocos SQL e checklist pÃ³s-aplicaÃ§Ã£o.
- Arquivos de saÃ­da do `ddl-analyze`: `.json` e `.html`, com resumo por tipo/tabela e filtros no HTML.
- O relatÃ³rio HTML do `ddl-analyze` inclui a seÃ§Ã£o **Tabelas priorizadas para correÃ§Ã£o** com `Prioridade` (`P0..P3`), `Ãndice de risco` e `Qtde`, alÃ©m de legenda de prioridade ao lado dos critÃ©rios de severidade.
- No `ddl-analyze --databases-batch`, tambÃ©m Ã© gerado um resumo consolidado: `batch_analysis_summary_*.json` e `.html`.
- `ddl-analyze` suporta dois modos de entrada: arquivo (`--input/--source`) ou conexÃ£o direta no banco (`--database` + opÃ§Ãµes de conexÃ£o).
- No `ddl-analyze --database`, o relatÃ³rio tambÃ©m inclui achados operacionais baseados nas tabelas de monitoramento do Firebird (`MON$`), como sinais de pressÃ£o de retenÃ§Ã£o transacional.
- `ddl-analyze` detecta Ã­ndices redundantes por prefixo (por exemplo, `(A)` vs `(A,B)`) como achados de otimizaÃ§Ã£o.
- `ddl-analyze` suporta modo em lote com `--databases-batch` (`*` e `?`) para analisar vÃ¡rios arquivos `.fdb`.
- `ddl-analyze` aceita `--ignore-table-prefix` (repetÃ­vel) e `--ignore-table-prefixes` (lista por vÃ­rgula) para reduzir ruÃ­do de tabelas tÃ©cnicas.
- `ddl-analyze` aceita `--severity-config` para sobrescrever severidade por cÃ³digo de achado.
- `ddl-analyze` aceita `--description` para incluir texto de contexto nos metadados do JSON e do HTML.
- `ddl-analyze` suporta `--volume-analysis on|off` (padrÃ£o `on`) para habilitar/desabilitar a anÃ¡lise de prioridade por volume.
- `ddl-analyze` usa estimativa por Ã­ndice como padrÃ£o e executa `COUNT(*)` exato apenas quando `--volume-count-exact on` Ã© informado explicitamente.
- Use `docs/examples/ddl-severity.sample.json` como referÃªncia de formato (cobre todos os cÃ³digos atuais).
- SaÃ­das reproduzÃ­veis de exemplo do `ddl-analyze`: `docs/examples/ddl-analyze-sample*.{sql,json,html}`.
- Valores aceitos de severidade: `critical`, `high`, `medium`, `low`.
- O formato do JSON Ã© somente em inglÃªs: `overrides`, `code`, `severity`.

## Principais OpÃ§Ãµes de ExportaÃ§Ã£o

- `--database` caminho do banco Firebird
- `--table` tabela de origem
- `--target-table` tabela destino nos `INSERT`s gerados
- `--output` arquivo ou diretÃ³rio de saÃ­da
- `--host` host do Firebird (padrÃ£o: `localhost`)
- `--port` porta do Firebird (padrÃ£o: `3050`)
- `--user` usuÃ¡rio do Firebird (padrÃ£o: `sysdba`)
- `--password` senha do Firebird (padrÃ£o: `masterkey`)
- `--charset` `WIN1252 | ISO8859_1 | UTF8 | NONE`
- `--filter` condiÃ§Ã£o simples (opcional)
- `--filter-file` lÃª condiÃ§Ã£o simples de arquivo
- `--query-file` lÃª `SELECT` completo de arquivo (modo avanÃ§ado)
- `--blob-format` `Hex | Base64`
- `--insert-mode` `insert | upsert` (`upsert` exige PK e escreve `MATCHING`)
- `--commit-every` adiciona `COMMIT` a cada N linhas
- `--progress-every` intervalo de progresso
- `--split-size-mb` tamanho da divisÃ£o em MB (`0` desativa)
- `--legacy-win1252` modo legado para `CHARSET NONE`
- `--sanitize-text` sanitiza textos antes de escrever o SQL
- `--escape-newlines` escapa quebras de linha em campos texto
- `--continue-on-error` continua exportando se uma linha falhar

Regras:
- NÃ£o combinar `--query-file` com `--filter` ou `--filter-file`.
- `--query-file` deve conter um `SELECT` completo.
- `--filter` aceita prefixo `WHERE` (removido automaticamente).

## Principais OpÃ§Ãµes de ImportaÃ§Ã£o

- `--database` caminho do banco Firebird
- `--input` arquivo SQL de entrada
- `--script` alias explÃ­cito de `--input`
- `--inputs-batch` padrÃ£o wildcard para mÃºltiplos arquivos SQL de entrada
- `--input-batch` alias de `--inputs-batch`
- `--scripts-batch` alias de `--inputs-batch`
- `--host` host do Firebird (padrÃ£o: `localhost`)
- `--port` porta do Firebird (padrÃ£o: `3050`)
- `--user` usuÃ¡rio do Firebird (padrÃ£o: `sysdba`)
- `--password` senha do Firebird (padrÃ£o: `masterkey`)
- `--progress-every` intervalo de progresso
- `--continue-on-error` continua importando apÃ³s erros de comando
- use apenas um modo de entrada por execuÃ§Ã£o: `--input/--script` ou `--inputs-batch`
- status no resumo do lote:
  - `Sucesso`: arquivo concluÃ­do sem erros de comandos SQL
  - `Sucesso com erros`: arquivo concluÃ­do com erros de comando usando `--continue-on-error`
  - `Falha`: arquivo interrompido por erro fatal

## Testes

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

### O Que os Testes Garantem Hoje

- ExportaÃ§Ã£o:
  - composiÃ§Ã£o segura/vÃ¡lida de `SELECT` (`table`, `columns`, `filter`, `query-file`);
  - consistÃªncia na geraÃ§Ã£o de SQL (`INSERT`/`UPSERT`, formatos de BLOB, escape de newline, `commit-every`);
  - cobertura de charset e comportamento legado (`UTF8`, `WIN1252`, `ISO8859_1`, `NONE` + modo legado);
  - exclusÃ£o de colunas calculadas/somente leitura e cenÃ¡rios de round-trip export/import.
- ImportaÃ§Ã£o / execuÃ§Ã£o SQL:
  - comportamento do parser streaming (`SET TERM`, comentÃ¡rios, literais de string);
  - comportamento fail-fast vs `--continue-on-error` e geraÃ§Ã£o de log de execuÃ§Ã£o;
  - fluxo de entrada em lote, validaÃ§Ã£o de parÃ¢metros e comportamento central de progresso/commit.
- Fluxos DDL:
  - `ddl-extract` gerando snapshot/DDL dos objetos principais;
  - `ddl-diff` detectando mudanÃ§as estruturais e sugerindo SQL;
  - `ddl-analyze` com validaÃ§Ãµes estruturais, override de severidade e composiÃ§Ã£o de resumos;
  - checks operacionais (`MON$`) com limites principais e agregaÃ§Ã£o do resumo em lote.
- Infra e CLI:
  - utilitÃ¡rios de detecÃ§Ã£o/resoluÃ§Ã£o de charset e divisÃ£o de arquivo de saÃ­da;
  - validaÃ§Ã£o de opÃ§Ãµes da CLI e classificaÃ§Ã£o contextual de erros.

### Lacunas de Cobertura e PrÃ³ximas Prioridades

- ResiliÃªncia operacional de `MON$` em edge cases de versÃ£o/permissÃ£o do Firebird.
- CombinaÃ§Ãµes mais profundas de dependÃªncias reais no `ddl-diff`, alÃ©m da ordenaÃ§Ã£o base jÃ¡ implementada.
- ValidaÃ§Ã£o de estresse de import/export em execuÃ§Ãµes longas e datasets de produÃ§Ã£o (pressÃ£o de recurso e estabilidade prolongada).
- Fluxos de lote com resultados mistos (falhas parciais e bases muito heterogÃªneas).

Testes de integraÃ§Ã£o:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Guia rÃ¡pido de troubleshooting

- Falha com erros SQL:
  - Consulte o log por execuÃ§Ã£o (`*_import_log_*.log`) e o resumo final.
- Problema de charset/acentuaÃ§Ã£o:
  - Defina `--charset` explÃ­cito; use `--legacy-win1252` sÃ³ em cenÃ¡rio legado `CHARSET NONE` confirmado.
- ExecuÃ§Ã£o/log muito grande:
  - Use opÃ§Ãµes de split/progresso e prefira saÃ­da redirecionada em CI.
- `ddl-analyze` sem achados operacionais:
  - Confirme modo por banco (`--database` ou `--databases-batch`) e permissÃµes de leitura em `MON$`.

## PadrÃ£o de DocumentaÃ§Ã£o

- [DOCS_STANDARD.md](./DOCS_STANDARD.md)

## DocumentaÃ§Ã£o por Comando

- `export`: [docs/commands/pt-BR/export.md](./docs/commands/pt-BR/export.md)
- `import`: [docs/commands/pt-BR/import.md](./docs/commands/pt-BR/import.md)
- `exec-sql`: [docs/commands/pt-BR/exec-sql.md](./docs/commands/pt-BR/exec-sql.md)
- `ddl-extract`: [docs/commands/pt-BR/ddl-extract.md](./docs/commands/pt-BR/ddl-extract.md)
- `ddl-diff`: [docs/commands/pt-BR/ddl-diff.md](./docs/commands/pt-BR/ddl-diff.md)
- `ddl-analyze`: [docs/commands/pt-BR/ddl-analyze.md](./docs/commands/pt-BR/ddl-analyze.md)
- CritÃ©rios de severidade/validaÃ§Ã£o do `ddl-analyze`: [docs/commands/pt-BR/ddl-analyze-severity-and-validations.md](./docs/commands/pt-BR/ddl-analyze-severity-and-validations.md)

## DependÃªncias

- `Scriban` Ã© usado para renderizar o HTML do `ddl-analyze` a partir de template.

## IsenÃ§Ã£o de Responsabilidade

O SkyFBTool Ã© distribuÃ­do sob a licenÃ§a MIT, "NO ESTADO EM QUE SE ENCONTRA", sem garantias de qualquer natureza.

Os autores nÃ£o se responsabilizam por:
- perda de dados
- corrupÃ§Ã£o de bancos
- falhas de execuÃ§Ã£o
- danos diretos ou indiretos
- uso incorreto
- impactos causados a terceiros

Valide sempre em ambiente de homologaÃ§Ã£o antes do uso em produÃ§Ã£o.

## LicenÃ§a

MIT

