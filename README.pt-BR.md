# SkyFBTool

[English](./README.md) | Português (Brasil)

SkyFBTool é uma CLI em .NET 8 para exportação/importação de dados Firebird (2.5 / 3.0 / 4.0 / 5.0), focada em grandes volumes, execução em streaming e segurança de charset.

## O Que Há de Novo

- [CHANGELOG.pt-BR.md](./CHANGELOG.pt-BR.md)
- [Releases](https://github.com/SkyInformatica/SkyFBTool/releases)

## Releases Automatizadas

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

## Recursos Principais

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

## Organização do Código

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
```

Observações:
- O idioma de saída/relatório de DDL usa detecção da cultura do SO (`English` padrão, `pt-BR` localizado).
- Arquivos de saída do `ddl-diff`: `.sql`, `.json` e `.html`.
- O relatório do `ddl-diff` inclui Top 10 achados críticos do alvo (com severidade), ordem sugerida de blocos SQL e checklist pós-aplicação.
- Arquivos de saída do `ddl-analyze`: `.json` e `.html`, com resumo por tipo/tabela e filtros no HTML.
- No `ddl-analyze --databases-batch`, também é gerado um resumo consolidado: `batch_analysis_summary_*.json` e `.html`.
- `ddl-analyze` suporta dois modos de entrada: arquivo (`--input/--source`) ou conexão direta no banco (`--database` + opções de conexão).
- `ddl-analyze` suporta modo em lote com `--databases-batch` (`*` e `?`) para analisar vários arquivos `.fdb`.
- `ddl-analyze` aceita `--ignore-table-prefix` (repetível) e `--ignore-table-prefixes` (lista por vírgula) para reduzir ruído de tabelas técnicas.
- `ddl-analyze` aceita `--severity-config` para sobrescrever severidade por código de achado.
- Use `docs/examples/ddl-severity.sample.json` como referência de formato (cobre todos os códigos atuais).
- Valores aceitos de severidade: `critical`, `high`, `medium`, `low`.
- O formato do JSON é somente em inglês: `overrides`, `code`, `severity`.

## Principais Opções de Exportação

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

## Principais Opções de Importação

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

## Testes

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

Testes de integração:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Padrão de Documentação

- [DOCS_STANDARD.md](./DOCS_STANDARD.md)

## Documentação por Comando

- `export`: [docs/commands/pt-BR/export.md](./docs/commands/pt-BR/export.md)
- `import`: [docs/commands/pt-BR/import.md](./docs/commands/pt-BR/import.md)
- `exec-sql`: [docs/commands/pt-BR/exec-sql.md](./docs/commands/pt-BR/exec-sql.md)
- `ddl-extract`: [docs/commands/pt-BR/ddl-extract.md](./docs/commands/pt-BR/ddl-extract.md)
- `ddl-diff`: [docs/commands/pt-BR/ddl-diff.md](./docs/commands/pt-BR/ddl-diff.md)
- `ddl-analyze`: [docs/commands/pt-BR/ddl-analyze.md](./docs/commands/pt-BR/ddl-analyze.md)

## Dependências

- `Scriban` é usado para renderizar o HTML do `ddl-analyze` a partir de template.

## Isenção de Responsabilidade

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
