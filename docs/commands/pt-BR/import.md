# Comando `import`

## O que faz
Executa scripts SQL no Firebird com parser em streaming (linha a linha), com suporte a:
- detecção de `SET NAMES`
- troca de delimitador com `SET TERM`
- execução por comando com continuação opcional em erro
- arquivo de log por execução

Use `import` para carga de dados, aplicação de scripts de schema e replay controlado de SQL.

## Quando usar
- DBA: execução operacional de SQL em volume com log auditável e modo best-effort quando necessário.
- Desenvolvedor: aplicação de scripts gerados em pipelines reproduzíveis de preparação de ambiente.

## Como usar
```powershell
SkyFBTool import --database CAMINHO.fdb --input ARQUIVO.sql [opções]
SkyFBTool import --database CAMINHO.fdb --inputs-batch "C:\exports\*.sql" [opções]
```

## Todas as opções
- `--database`: caminho do banco Firebird de destino.
- `--input`: arquivo SQL de entrada (modo arquivo único).
- `--script`: alias explícito de `--input`.
- `--inputs-batch`: padrão wildcard para múltiplos arquivos SQL (modo lote).
- `--input-batch`: alias de `--inputs-batch`.
- `--scripts-batch`: alias de `--inputs-batch`.
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--progress-every`: exibe progresso a cada N linhas/comandos processados.
- `--continue-on-error`: continua após falhas de comandos SQL (modo best-effort).

## Regras
- Use apenas um modo de entrada por execução:
  - arquivo único: `--input/--script`
  - lote: `--inputs-batch` (ou aliases)
- Não combine opções de arquivo único com modo lote na mesma execução.

## Modelo de execução (importante)
- O parser é streaming e consciente de SQL:
  - ignora comentários (`-- ...`, `/* ... */`)
  - respeita literais de string (incluindo aspas escapadas)
  - suporta troca dinâmica de delimitador via `SET TERM`
- Comandos são executados um a um, em contexto transacional.
- Índices de tabelas podem ser gerenciados temporariamente no fluxo de execução.
- O resumo final apresenta:
  - total de linhas processadas
  - total de comandos executados
  - tempo decorrido
  - vazão média de comandos

## Comportamento de progresso no console
- Terminal interativo (TTY):
  - linha dinâmica de progresso é atualizada em tempo real (`processado`, `comandos`, `velocidade`, `tempo`).
  - checkpoints fixos são impressos a cada 50.000 unidades processadas ou 30 segundos (o que ocorrer primeiro).
- Saída redirecionada / CI:
  - renderização dinâmica em linha única é desativada.
  - progresso/checkpoints são emitidos em linhas fixas para melhor leitura em log.
- O resumo final sempre é exibido com totais, tempo decorrido, vazão e total de erros.

## Semântica de erro
- Sem `--continue-on-error`:
  - a primeira falha de comando SQL interrompe o arquivo com exceção.
- Com `--continue-on-error`:
  - comandos com erro são registrados em log e a execução continua.
  - o status final pode indicar conclusão com erros.

## Política de retry para falhas transitórias
- A execução de comandos SQL aplica retry automático para falhas transitórias (até 3 tentativas).
- Casos transitórios típicos incluem deadlock/conflito de update e instabilidade temporária de conexão/engine.
- Erros não transitórios não entram em retry.
- Se todas as tentativas falharem:
  - sem `--continue-on-error`, a execução é interrompida;
  - com `--continue-on-error`, o comando é registrado como falha e a importação continua.

## Semântica de status no lote
No modo em lote, o resumo final diferencia:
- `Sucesso`: arquivo concluído sem erros de comandos SQL.
- `Sucesso com erros`: arquivo concluído, mas com um ou mais comandos SQL com falha em `--continue-on-error`.
- `Falha`: execução do arquivo interrompida por erro fatal.

## Log de execução
- Um arquivo de log é sempre gerado por execução com nome único:
  - `*_import_log_*.log`
- O log contém início/fim, erros de comando e status final.
- Em incidentes operacionais, esse arquivo é a fonte principal de auditoria.

## Recomendações operacionais
- Para scripts de alto risco, execute primeiro em homologação com mesma versão/charset do ambiente alvo.
- Em produção, prefira:
  - backup/ponto de restauração explícito
  - janela controlada de execução
  - `--continue-on-error` somente quando conclusão parcial for aceitável
- Para scripts grandes:
  - use `--progress-every` para observabilidade
  - particione arquivos na origem se precisar granularidade melhor de rollback/reexecução

## Exemplos
```powershell
SkyFBTool import --database "C:\dados\erp.fdb" --input "C:\exports\clientes.sql"
SkyFBTool import --database "C:\dados\erp.fdb" --script ".\sql\patch.sql" --progress-every 1000
SkyFBTool import --database "C:\dados\erp.fdb" --inputs-batch "C:\exports\*.sql" --continue-on-error
```
