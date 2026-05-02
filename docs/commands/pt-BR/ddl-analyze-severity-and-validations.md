# Critérios de Severidade e Validações do `ddl-analyze`

Este documento descreve, de forma precisa, quais validações o `ddl-analyze` executa e como cada achado é classificado em severidade.

## Escopo da análise

- Modo por arquivo (`--input/--source`): validações estruturais de schema.
- Modo por banco (`--database`): validações estruturais + achados operacionais (`MON$`).
- Modo em lote (`--databases-batch`): mesmo comportamento do modo por banco, aplicado por arquivo `.fdb`.

## Níveis de severidade

- `critical`: inconsistência estrutural grave ou alto risco de integridade.
- `high`: risco elevado de qualidade/operação, sem evidência direta de corrupção imediata.
- `medium`: risco relevante de performance/operação que exige acompanhamento.
- `low`: oportunidade de simplificação/otimização com baixo risco imediato.

## Regra de override de severidade

- A severidade padrão por código pode ser sobrescrita com `--severity-config`.
- Formato de referência: `docs/examples/ddl-severity.sample.json`.
- Valores válidos: `critical`, `high`, `medium`, `low`.

## Matriz de validações estruturais

| Código | Severidade padrão | O que valida | Critério usado hoje |
|---|---|---|---|
| `TABELA_SEM_COLUNAS` | `critical` | Tabela sem colunas | `tabela.Colunas.Count == 0` |
| `COLUNA_DUPLICADA` | `critical` | Coluna repetida na mesma tabela | Agrupamento por nome de coluna (case-insensitive), contagem > 1 |
| `TIPO_DESCONHECIDO` | `high` | Tipo SQL não mapeado | `TipoSql` começa com `TYPE_` |
| `TABELA_SEM_PK` | `high` | Tabela sem PK | `tabela.ChavePrimaria is null` |
| `PK_SEM_COLUNAS` | `critical` | PK sem lista de colunas | `ChavePrimaria.Colunas.Count == 0` |
| `PK_REFERENCIA_COLUNA_INEXISTENTE` | `critical` | PK aponta para coluna inexistente | Coluna da PK não encontrada no dicionário de colunas da tabela |
| `FK_SEM_COLUNAS` | `critical` | FK sem colunas locais e/ou de referência | `fk.Colunas.Count == 0` ou `fk.ColunasReferencia.Count == 0` |
| `FK_CARDINALIDADE_INVALIDA` | `critical` | FK com cardinalidade local/referência diferente | `fk.Colunas.Count != fk.ColunasReferencia.Count` |
| `FK_COLUNA_LOCAL_INEXISTENTE` | `critical` | FK usa coluna local inexistente | Coluna local da FK não existe na tabela |
| `FK_TABELA_REFERENCIA_INEXISTENTE` | `critical` | FK referencia tabela inexistente | Tabela de referência não encontrada no mapa de tabelas |
| `FK_COLUNA_REFERENCIA_INEXISTENTE` | `critical` | FK referencia coluna inexistente na tabela alvo | Coluna de referência da FK não encontrada |
| `FK_SEM_INDICE_COBERTURA` | `medium` | FK sem índice de cobertura local | Não há índice de suporte da própria FK e nenhum índice regular cobre o prefixo das colunas da FK |
| `INDICE_SEM_COLUNAS` | `high` | Índice sem colunas | `indice.Colunas.Count == 0` |
| `INDICE_COLUNA_INEXISTENTE` | `high` | Índice aponta para coluna inexistente | Coluna do índice não encontrada |
| `INDICE_DUPLICADO` | `low` | Índices com mesma assinatura funcional | Mesma assinatura (`U/N`, `A/D`, lista ordenada de colunas) |
| `INDICE_REDUNDANTE_PREFIXO` | `medium` | Índice possivelmente redundante por prefixo | Índice curto é prefixo de índice maior, mesma direção, ambos não únicos |
| `FK_DUPLICADA` | `low` | FKs com mesma assinatura funcional | Mesma assinatura (colunas locais, tabela/colunas referência, regras update/delete) |
| `OPERACIONAL_VOLUME_PRIORIDADE_ALTA` | `high` | Tabela de alto volume com achados concentrados | Registros estimados >= 10.000.000 e achados na tabela >= 3 |
| `OPERACIONAL_VOLUME_PRIORIDADE_MEDIA` | `medium` | Tabela de volume relevante com achados recorrentes | Registros estimados >= 1.000.000 e achados na tabela >= 2 |
| `OPERACIONAL_VOLUME_PRIORIDADE_BAIXA` | `low` | Tabela de volume intermediário com achados | Registros estimados >= 500.000 e achados na tabela >= 1 |

## Matriz de validações operacionais (`MON$`) - apenas `--database`

| Código | Severidade padrão | O que valida | Limite atual | Significado prático |
|---|---|---|---|---|
| `OPERACIONAL_GAP_OIT_OAT_ELEVADO` | `critical` | Pressão transacional por diferença OAT-OIT | `OAT - OIT >= 200000` | Sinal forte de garbage collection bloqueado; alto risco de degradação contínua e investigação urgente. |
| `OPERACIONAL_GAP_OIT_OAT_ACIMA_DO_ESPERADO` | `high` | Pressão transacional acima do esperado | `OAT - OIT >= 50000` | A limpeza está atrasada em relação à carga; provável presença de transações longas ou housekeeping insuficiente. |
| `OPERACIONAL_GAP_OIT_OAT_MODERADO` | `medium` | Pressão transacional moderada | `OAT - OIT >= 10000` | Sinal inicial de pressão; acompanhar tendência e agir antes de escalar. |
| `OPERACIONAL_GAP_OAT_OST_ELEVADO` | `high` | Backlog snapshot elevado por diferença OST-OAT | `OST - OAT >= 200000` | Leitores snapshot longos provavelmente estão segurando versões antigas; revisar perfil de leitura/relatórios. |
| `OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO` | `medium` | Backlog snapshot acima do esperado | `OST - OAT >= 50000` | Retenção snapshot acima da faixa saudável; monitorar e ajustar leituras longas. |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA_CRITICA` | `critical` | Transação ativa longa crítica | idade >= `720` minutos | Há transação aberta por muitas horas; requer ação imediata para evitar pressão de retenção e lock. |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA` | `high` | Transação ativa longa | idade >= `120` minutos | Duração já arriscada para operação normal; investigar em curto prazo. |
| `OPERACIONAL_TRANSACAO_ATIVA_ACIMA_DO_ESPERADO` | `medium` | Duração acima do esperado | idade >= `30` minutos | Duração acima da linha de base esperada; revisar padrão da rotina e acompanhar tendência. |

### O que significa cada métrica MON$

- `OIT` (`mon$oldest_transaction`): ID da transação mais antiga que ainda não pode ser coletada pelo garbage collection.
- `OAT` (`mon$oldest_active`): ID da transação ativa mais antiga no momento.
- `OST` (`mon$oldest_snapshot`): ID da transação snapshot mais antiga que ainda mantém versões antigas visíveis.
- `NXT` (`mon$next_transaction`): próximo ID de transação que será alocado.

### Como interpretar os achados por gap

- `OAT - OIT` (pressão de retenção):
  - Valores altos normalmente indicam transações antigas impedindo limpeza de versões.
  - Impacto prático: acúmulo de garbage, cadeias de versões maiores e piora de performance.
- `OST - OAT` (backlog de snapshot):
  - Valores altos normalmente indicam leitores snapshot longos segurando versões antigas.
  - Impacto prático: limpeza mais lenta e maior pressão de armazenamento/IO.

### Como é calculado o achado de transação ativa longa

- Consulta usada: `MIN(mon$timestamp)` em `mon$transactions` com `mon$state = 1`, excluindo a conexão atual.
- O analisador calcula `DateTime.UtcNow - MIN(mon$timestamp)` e classifica por limite:
  - `>= 720 min` -> `critical`
  - `>= 120 min` -> `high`
  - `>= 30 min` -> `medium`

### Exemplos práticos de leitura

- Exemplo A: `OIT=1000`, `OAT=260000` -> `OAT - OIT = 259000` -> `OPERACIONAL_GAP_OIT_OAT_ELEVADO` (`critical`).
- Exemplo B: `OAT=500000`, `OST=560500` -> `OST - OAT = 60500` -> `OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO` (`medium`).
- Exemplo C: transação ativa mais antiga começou há 3h10 -> `190 min` -> `OPERACIONAL_TRANSACAO_ATIVA_LONGA` (`high`).

### Escopo e limitações

- Checks operacionais saem apenas no modo por banco (`--database`, e por banco no `--databases-batch`).
- No modo por arquivo (`--input/--source`) não existe contexto transacional ao vivo; por isso não há `OPERACIONAL_*`.
- Se a coleta MON$ falhar (permissão/acesso/consulta), os achados estruturais continuam sendo gerados e o relatório marca a análise operacional como `indisponível`, com o motivo no resumo.

### Origem das métricas operacionais

- `mon$database`: `mon$oldest_transaction`, `mon$oldest_active`, `mon$oldest_snapshot`, `mon$next_transaction`
- `mon$transactions`: `MIN(mon$timestamp)` para transações ativas (`mon$state = 1`), excluindo a conexão atual.

## Observações de classificação (melhores práticas)

- Achados `critical` correspondem a erros de consistência estrutural (schema inválido/incompleto) ou sinais operacionais extremos.
- Achados `high` apontam riscos relevantes para estabilidade e governança de dados.
- Achados `medium` sinalizam problemas que tendem a degradar desempenho/operação e merecem plano de correção.
- Achados `low` representam redundância/otimização e devem ser validados por plano de execução e carga real antes de remoção.

## Importante para interpretação

- No modo `--input/--source`, não há coleta de `MON$`; portanto, os códigos `OPERACIONAL_*` não aparecem.
- Se a coleta operacional falhar no modo `--database` (permissão/acesso/erro de consulta), a análise estrutural continua e o relatório é gerado com a análise operacional marcada como `indisponível`.
- `FK_SEM_INDICE_COBERTURA` não é emitido quando a FK já possui índice de suporte vinculado à constraint (por exemplo, `RDB$RELATION_CONSTRAINTS.RDB$INDEX_NAME` no modo por banco, ou índice equivalente no modo por snapshot SQL).
- Achados de prioridade por volume são emitidos apenas no modo por banco e usam estimativa por índice como padrão (coleta best-effort); `COUNT(*)` exato é opcional via `--volume-count-exact on`.
