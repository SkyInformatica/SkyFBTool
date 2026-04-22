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

- Comandos `export` e `import`
- Comandos `ddl-extract` e `ddl-diff` para comparação de schema
- Exportação/importação em streaming para arquivos SQL grandes
- `--filter`, `--filter-file` e modo avançado `--query-file`
- Remapeamento de tabela destino com `--target-table`
- `--blob-format` (`Hex` ou `Base64`)
- `--commit-every` e `--progress-every` configuráveis
- Divisão de arquivo com `--split-size-mb` (padrão: 100 MB)
- Modo legado de charset para `CHARSET NONE` com `--legacy-win1252`
- Aviso para arquivos grandes de filtro/consulta (> 64 KB)

## Organização do Código

- `Program.cs`: ponto de entrada mínimo
- `Cli/CliApp.cs`: roteamento da CLI + ajuda
- `Cli/Commands/*`: um arquivo por comando (`export`, `import`, `ddl-extract`, `ddl-diff`)
- `Cli/Common/*`: utilitários compartilhados de parsing de argumentos
- `Services/*`: lógica por contexto (Export, Import, Ddl)
- `Infra/*`: adaptadores técnicos (conexão, encoding, arquivos)

## Uso

```powershell
SkyFBTool export [opções]
SkyFBTool import [opções]
SkyFBTool ddl-extract [opções]
SkyFBTool ddl-diff [opções]
```

### Exemplo de exportação

```powershell
SkyFBTool export --database "C:\dados\exemplo.fdb" --table "TABELA_EXEMPLO" --output "C:\exports\" --commit-every 10000
```

### Exemplo de importação

```powershell
SkyFBTool import --database "C:\dados\exemplo.fdb" --input "C:\exports\tabela_exemplo.sql" --continue-on-error
```

### Exemplos de extração e diff de DDL

```powershell
SkyFBTool ddl-extract --database "C:\dados\origem.fdb" --output "C:\ddl\origem"
SkyFBTool ddl-extract --database "C:\dados\alvo.fdb" --output "C:\ddl\alvo"
SkyFBTool ddl-diff --source "C:\ddl\origem.schema.json" --target "C:\ddl\alvo.schema.json" --output "C:\ddl\comparacao"
```

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
- `--host` host do Firebird (padrão: `localhost`)
- `--port` porta do Firebird (padrão: `3050`)
- `--user` usuário do Firebird (padrão: `sysdba`)
- `--password` senha do Firebird (padrão: `masterkey`)
- `--progress-every` intervalo de progresso
- `--continue-on-error` continua importando após erros de comando

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
