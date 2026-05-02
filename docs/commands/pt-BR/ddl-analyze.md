# Comando `ddl-analyze`

## O que faz
Analisa risco estrutural do schema e gera:
- JSON de análise (`.json`)
- relatório HTML (`.html`)
- resumo consolidado em lote (`batch_analysis_summary_*.json/.html`) no modo batch

Quando usado com `--database`, também executa checks operacionais nas tabelas de monitoramento do Firebird (`MON$`) e adiciona esses achados ao mesmo relatório.
No modo por banco, usa estimativa leve de volume por índice para priorizar impacto dos achados.
No modo por banco, os metadados do relatório também incluem a data estimada da última manutenção via `MON$DATABASE.MON$CREATION_DATE` (criação/último restore do banco).

Também detecta redundância de índice por prefixo (por exemplo, `(A)` potencialmente redundante quando `(A,B)` já existe na mesma direção).

No relatório HTML, o `ddl-analyze` também apresenta:
- tabela **Tabelas priorizadas para correção** (por escopo/tabela), com `Prioridade` (`P0..P3`), `Índice de risco` e `Qtde`;
- legenda de prioridade (`P0..P3`) ao lado dos critérios de severidade para facilitar decisão rápida do DBA.

## Quando usar
- DBA: priorizar correções com base em severidade, índice de risco e concentração por tabela.
- Desenvolvedor: validar gate de qualidade de schema e detectar regressões estruturais antes do deploy.

## Como usar
```powershell
SkyFBTool ddl-analyze --input ENTRADA --output PREFIXO [opções]
SkyFBTool ddl-analyze --database CAMINHO.fdb --output PREFIXO [opções]
SkyFBTool ddl-analyze --databases-batch "C:\dados\*.fdb" --output DIRETÓRIO [opções]
```

## Todas as opções
- `--input`: entrada por arquivo (`.schema.json` ou `.sql`).
- `--source`: alias de `--input`.
- `--database`: entrada por banco único.
- `--databases-batch`: wildcard para entrada em lote (`*`, `?`).
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--charset`: charset opcional da conexão.
- `--output`: prefixo/arquivo base/diretório de saída.
- `--ignore-table-prefix`: ignora prefixo de tabela (repetível).
- `--ignore-table-prefixes`: lista de prefixos ignorados separados por vírgula.
- `--severity-config`: JSON de override de severidade.
- `--description`: texto livre incluído nos metadados do relatório JSON/HTML.
- `--volume-analysis`: `on` (padrão) ou `off` para análise SQL de prioridade por volume.
- `--volume-count-exact`: `on` ou `off` (padrão: `off`). Quando `on`, executa `COUNT(*)` exato por tabela no lugar da estimativa por índice.

## Regras
- Use apenas um modo de entrada: arquivo (`--input/--source`) ou banco único (`--database`) ou lote (`--databases-batch`).
- `--database` não aceita wildcard; wildcard é via `--databases-batch`.
- Checks operacionais estão disponíveis apenas no modo por banco (`--database`), não no modo por arquivo (`--input/--source`).
- A coleta operacional de `MON$` no modo por banco é best-effort:
  - se falhar (permissão/versão/consulta/timeout), a análise estrutural ainda conclui normalmente;
  - o relatório marca a análise operacional como `indisponível` e registra o motivo no resumo.
- A estimativa de volume no modo por banco é best-effort; se falhar ou estourar timeout, a análise continua sem os achados de prioridade por volume.

## Exemplos
```powershell
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --output "C:\ddl\analise"
SkyFBTool ddl-analyze --database "C:\dados\origem.fdb" --output "C:\ddl\analise_do_banco"
SkyFBTool ddl-analyze --databases-batch "C:\dados\*.fdb" --output "C:\ddl\analises_lote\"
SkyFBTool ddl-analyze --input "C:\ddl\origem.sql" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$ --severity-config ".\docs\examples\ddl-severity.sample.json" --output "C:\ddl\analise_custom"
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --description "análise no banco XYZ" --output "C:\ddl\analise_com_contexto"
```

## Exemplos de relatório
- [Exemplo simples de relatório em HTML](../../examples/ddl-analyze-sample.html)
- [Exemplo rico de relatório em HTML](../../examples/ddl-analyze-sample-rich.html)
- [Exemplo de relatório resumo em lote (HTML)](../../examples/ddl-analyze-batch-summary-sample.html)

## Arquivos de exemplo reproduzíveis
- Entrada simples: `docs/examples/ddl-analyze-sample-input.sql`
- Saída simples: `docs/examples/ddl-analyze-sample.html` e `docs/examples/ddl-analyze-sample.json`
- Entrada rica: `docs/examples/ddl-analyze-sample-rich-input.sql`
- Saída rica: `docs/examples/ddl-analyze-sample-rich.html` e `docs/examples/ddl-analyze-sample-rich.json`
- Saída de resumo em lote: `docs/examples/ddl-analyze-batch-summary-sample.html` e `docs/examples/ddl-analyze-batch-summary-sample.json`

## Critérios de classificação e validações
- [Matriz de severidade e validações do `ddl-analyze`](./ddl-analyze-severity-and-validations.md)
