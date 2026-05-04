# Comando `ddl-diff`

## O que faz
Compara duas entradas de schema e gera:
- SQL de ajuste (`.sql`) para aproximar o alvo da origem
- diff estruturado (`.json`) para tooling/auditoria
- relatório visual (`.html`) para revisão humana

`ddl-diff` foi desenhado para fluxos controlados de sincronização de schema (promoções, auditorias, planejamento de migração).

Por padrão, o diff ignora diferenças de `DOMAIN` para manter a revisão mais prática. Use `--include-domains` quando quiser comparar domains também. O diff continua cobrindo sequências/geradores, views, procedimentos, funções armazenadas, gatilhos, constraints únicas e constraints `CHECK` além de tabelas, colunas, PKs, FKs e índices de usuário.

## Quando usar
- DBA: avaliar drift de schema e gerar SQL de ajuste revisado antes do rollout.
- Desenvolvedor: validar impacto de migrações e manter alinhamento explícito entre origem e alvo.

## Como usar
```powershell
SkyFBTool ddl-diff --source ORIGEM --target ALVO --output PREFIXO
```

## Todas as opções
- `--source`: entrada de origem (`.schema.json` ou `.sql`).
- `--source-ddl`: alias de `--source`.
- `--target`: entrada de alvo (`.schema.json` ou `.sql`).
- `--target-ddl`: alias de `--target`.
- `--output`: prefixo/arquivo base/diretório de saída.
- `--include-domains`: inclui domains na comparação em vez de ignorá-los por padrão.

## Regras e orientação operacional
- Mantenha papéis de origem/alvo explícitos:
  - **origem** = modelo desejado
  - **alvo** = modelo atual que será ajustado
- Prefira entradas `.schema.json` vindas de `ddl-extract` para comparações determinísticas.
- Se usar entrada `.sql`, mantenha convenções de extração/parser consistentes nos dois lados.
- Sempre revise o `.sql` gerado antes de executar em produção.
- Use o relatório `.html` para validar ordem de operações e mudanças de maior risco antes da aplicação.

## Modelo de ordenação dos comandos
O SQL gerado é ordenado por profundidade de dependência para reduzir erros na aplicação prática:
1. `ALTER TABLE ... DROP CONSTRAINT`
2. `CREATE DOMAIN`
3. `CREATE SEQUENCE`
4. `CREATE PROCEDURE`
5. `CREATE FUNCTION`
6. `CREATE TABLE`
7. `CREATE VIEW`
8. `ALTER TABLE ... ADD <coluna>`
9. `ALTER TABLE ... ALTER COLUMN`
10. `ADD CONSTRAINT ... PRIMARY KEY`
11. `ADD CONSTRAINT ... UNIQUE`
12. `ADD CONSTRAINT ... CHECK`
13. `CREATE TRIGGER`
14. `CREATE INDEX`
15. `ADD CONSTRAINT ... FOREIGN KEY`

Essa ordenação é determinística e mantém objetos base antes dos dependentes, além de deixar FKs e triggers após a fundação do schema.

## Interpretação prática das saídas
- `.sql`: candidato executável de ajuste (a ferramenta não aplica automaticamente).
- `.json`: lista estruturada de diferenças para automação/integração.
- `.html`: revisão visual priorizada com contexto e sequência sugerida.

## Fluxo recomendado
1. Gere snapshots com `ddl-extract` nos dois ambientes.
2. Execute `ddl-diff` entre snapshots de origem e alvo.
3. Revise `.html` e `.sql` com DBA/dev, incluindo domínios apenas quando `--include-domains` estiver habilitado e constraints adicionais quando presentes.
4. Aplique em homologação.
5. Reexecute `ddl-diff` para confirmar convergência.

## Exemplos
```powershell
SkyFBTool ddl-diff --source "C:\ddl\origem.schema.json" --target "C:\ddl\alvo.schema.json" --output "C:\ddl\comparacao"
SkyFBTool ddl-diff --source-ddl "C:\ddl\origem.sql" --target-ddl "C:\ddl\alvo.sql" --output "C:\ddl\comparacao_sql"
SkyFBTool ddl-diff --source "docs\examples\ddl-diff-sample-source.sql" --target "docs\examples\ddl-diff-sample-target.sql" --output "docs\examples\ddl-diff-sample"
```
