# Comando `exec-sql`

## O que faz
Executa um script SQL no Firebird usando o mesmo motor de execução do `import`.

Use `exec-sql` quando o objetivo for execução operacional de script (patch de schema, ajuste de dados, manutenção), e não fluxo de exportação/importação de dados.

## Quando usar
- DBA: execução de manutenção com intenção operacional explícita e log de auditoria ao final.
- Desenvolvedor: aplicação determinística de patch durante bootstrap de ambiente ou ensaio de migração.

## Como usar
```powershell
SkyFBTool exec-sql --database CAMINHO.fdb --script ARQUIVO.sql [opções]
```

## Todas as opções
`exec-sql` usa o mesmo parser/opções de `import`:
- `--database`: banco Firebird de destino.
- `--input`: arquivo SQL de entrada.
- `--script`: alias explícito de `--input` (recomendado por clareza em manutenção).
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--progress-every`: intervalo de progresso no console (somente observabilidade).
- `--continue-on-error`: continua após falhas de comandos SQL (execução best-effort).

## Regras e orientação operacional
- Use apenas um arquivo de entrada por execução (`--input` ou `--script`).
- Prefira `--script` em `exec-sql` para que logs/comandos comuniquem claramente intenção de manutenção.
- Use `--continue-on-error` apenas quando execução parcial for aceitável e houver validação pós-execução.
- Sempre revise o log gerado (`*_import_log_*.log`) ao usar `--continue-on-error`.
- Para scripts de alto risco (DDL em produção), execute primeiro em homologação e mantenha estratégia explícita de rollback.

## Diferença prática para `import`
- `import`: voltado para fluxo de ingestão SQL/dados, incluindo modo batch.
- `exec-sql`: mesmo motor, com nomenclatura operacional para patch/manutenção.

## Exemplos
```powershell
SkyFBTool exec-sql --database "C:\dados\erp.fdb" --script ".\sql\patch_2026_04.sql"
SkyFBTool exec-sql --database "C:\dados\erp.fdb" --script ".\sql\rebuild_indexes.sql" --progress-every 500
SkyFBTool exec-sql --database "C:\dados\erp.fdb" --script ".\sql\cleanup_best_effort.sql" --continue-on-error
```
