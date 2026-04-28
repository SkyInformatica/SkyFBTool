# Comando `ddl-analyze`

## O que faz
Analisa risco estrutural do schema e gera:
- JSON de análise (`.json`)
- relatório HTML (`.html`)
- resumo consolidado em lote (`batch_analysis_summary_*.json/.html`) no modo batch

Quando usado com `--database`, também executa checks operacionais nas tabelas de monitoramento do Firebird (`MON$`) e adiciona esses achados ao mesmo relatório.

Também detecta redundância de índice por prefixo (por exemplo, `(A)` potencialmente redundante quando `(A,B)` já existe na mesma direção).

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

## Regras
- Use apenas um modo de entrada: arquivo (`--input/--source`) ou banco único (`--database`) ou lote (`--databases-batch`).
- `--database` não aceita wildcard; wildcard é via `--databases-batch`.
- Checks operacionais estão disponíveis apenas no modo por banco (`--database`), não no modo por arquivo (`--input/--source`).

## Exemplos
```powershell
SkyFBTool ddl-analyze --input "C:\ddl\origem.schema.json" --output "C:\ddl\analise"
SkyFBTool ddl-analyze --database "C:\dados\origem.fdb" --output "C:\ddl\analise_do_banco"
SkyFBTool ddl-analyze --databases-batch "C:\dados\*.fdb" --output "C:\ddl\analises_lote\"
SkyFBTool ddl-analyze --input "C:\ddl\origem.sql" --ignore-table-prefix LOG_ --ignore-table-prefixes TMP_,IBE$ --severity-config ".\docs\examples\ddl-severity.sample.json" --output "C:\ddl\analise_custom"
```

## Exemplo de relatório
![Exemplo de relatório do ddl-analyze](../../examples/ddl-analyze-report-example.png)

## Exemplo de resumo em lote
![Exemplo de resumo em lote do ddl-analyze](../../examples/ddl-analyze-batch-summary-example.png)
