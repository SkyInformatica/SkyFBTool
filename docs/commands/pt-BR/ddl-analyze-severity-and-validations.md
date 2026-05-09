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

| Código | Severidade padrão | O que valida | Critério usado hoje | Explicação prática | Exemplo fictício |
|---|---|---|---|---|---|
| `TABELA_SEM_COLUNAS` | `critical` | Tabela sem colunas | `tabela.Colunas.Count == 0` | A tabela existe no schema, mas não possui colunas válidas. | `CREATE TABLE LOG_AUDITORIA ();` |
| `COLUNA_DUPLICADA` | `critical` | Coluna repetida na mesma tabela | Agrupamento por nome de coluna (case-insensitive), contagem > 1 | A mesma coluna foi declarada mais de uma vez na tabela. | `CLIENTES(ID INT, NOME VARCHAR(60), NOME VARCHAR(80))` |
| `TIPO_DESCONHECIDO` | `high` | Tipo SQL não mapeado | `TipoSql` começa com `TYPE_` | O tipo da coluna não foi reconhecido/mapeado para Firebird. | `TOTAL TYPE_99` em tabela de faturamento. |
| `TABELA_SEM_PK` | `high` | Tabela sem PK | `tabela.ChavePrimaria is null` | Tabela sem identificador primário confiável. | `PEDIDOS` com milhões de registros e sem `PRIMARY KEY`. |
| `PK_SEM_COLUNAS` | `critical` | PK sem lista de colunas | `ChavePrimaria.Colunas.Count == 0` | Constraint de PK existe, mas sem colunas válidas. | `PK_PEDIDOS` criada sem colunas associadas. |
| `PK_REFERENCIA_COLUNA_INEXISTENTE` | `critical` | PK aponta para coluna inexistente | Coluna da PK não encontrada no dicionário de colunas da tabela | A PK referencia campo inexistente. | PK usa `ID_PEDIDO`, mas a tabela tem apenas `COD_PEDIDO`. |
| `FK_SEM_COLUNAS` | `critical` | FK sem colunas locais e/ou de referência | `fk.Colunas.Count == 0` ou `fk.ColunasReferencia.Count == 0` | FK sem mapeamento relacional completo. | `FK_ITENS_PEDIDO` sem mapeamento de campos. |
| `FK_CARDINALIDADE_INVALIDA` | `critical` | FK com cardinalidade local/referência diferente | `fk.Colunas.Count != fk.ColunasReferencia.Count` | Quantidade de colunas local e referenciada não fecha. | FK local `(EMPRESA_ID, PEDIDO_ID)` para PK `(PEDIDO_ID)`. |
| `FK_COLUNA_LOCAL_INEXISTENTE` | `critical` | FK usa coluna local inexistente | Coluna local da FK não existe na tabela | A FK usa campo que não existe na tabela filha. | FK em `ITENS_PEDIDO` usa `FILIAL_ID`, mas coluna não existe. |
| `FK_TABELA_REFERENCIA_INEXISTENTE` | `critical` | FK referencia tabela inexistente | Tabela de referência não encontrada no mapa de tabelas | A FK aponta para tabela pai ausente no schema. | FK referencia `CLIENTE_MASTER`, mas só existe `CLIENTES`. |
| `FK_COLUNA_REFERENCIA_INEXISTENTE` | `critical` | FK referencia coluna inexistente na tabela alvo | Coluna de referência da FK não encontrada | A FK aponta para coluna ausente na tabela pai. | FK para `CLIENTES(ID_CLIENTE)`, mas a tabela pai tem `CLIENTE_ID`. |
| `FK_SEM_INDICE_COBERTURA` | `medium` | FK sem índice de cobertura local | Não há índice de suporte da própria FK e nenhum índice regular cobre o prefixo das colunas da FK | FK sem índice útil para join/validação relacional. | `PEDIDOS.CLIENTE_ID` com FK sem índice iniciando por `CLIENTE_ID`. |
| `INDICE_SEM_COLUNAS` | `high` | Índice sem colunas | `indice.Colunas.Count == 0` | Índice definido sem campos válidos. | Índice `IDX_MOV` criado vazio por script incompleto. |
| `INDICE_COLUNA_INEXISTENTE` | `high` | Índice aponta para coluna inexistente | Coluna do índice não encontrada | Índice inclui coluna ausente na tabela. | Índice em `VENDAS(DATA_EMISSAO)` sem a coluna `DATA_EMISSAO`. |
| `INDICE_DUPLICADO` | `low` | Índices com mesma assinatura funcional | Mesma assinatura (`U/N`, `A/D`, lista ordenada de colunas) | Dois índices entregam a mesma função prática. | `IDX_A` e `IDX_B` em `(CLIENTE_ID, DATA)` ambos não únicos/ASC. |
| `INDICE_REDUNDANTE_PREFIXO` | `medium` | Índice possivelmente redundante por prefixo | Índice curto é prefixo de índice maior, mesma direção, ambos não únicos | Índice menor tende a ser redundante. | `(CLIENTE_ID)` e `(CLIENTE_ID, DATA)` na mesma direção. |
| `FK_DUPLICADA` | `low` | FKs com mesma assinatura funcional | Mesma assinatura (colunas locais, tabela/colunas referência, regras update/delete) | Há FKs diferentes repetindo a mesma regra relacional. | `FK_PED_CLIENTE_1` e `FK_PED_CLIENTE_2` com mesmos campos e regras. |
| `OPERACIONAL_VOLUME_PRIORIDADE_ALTA` | `high` | Tabela de alto volume com achados concentrados | Registros estimados >= 10.000.000 e achados na tabela >= 3 | Tabela muito grande com vários achados; impacto potencial alto. | `MOV_ESTOQUE` com 25M linhas e 4 achados estruturais. |
| `OPERACIONAL_VOLUME_PRIORIDADE_MEDIA` | `medium` | Tabela de volume relevante com achados recorrentes | Registros estimados >= 1.000.000 e achados na tabela >= 2 | Tabela grande com recorrência de achados; prioridade relevante. | `ITENS_NF` com 2,3M linhas e 2 achados. |
| `OPERACIONAL_VOLUME_PRIORIDADE_BAIXA` | `low` | Tabela de volume intermediário com achados | Registros estimados >= 500.000 e achados na tabela >= 1 | Tabela de porte médio com achado; priorização preventiva. | `LOG_EVENTOS` com 700k linhas e 1 achado. |

## Matriz de validações operacionais (`MON$`) - apenas `--database`

| Código | Severidade padrão | O que valida | Limite atual | Significado prático | Exemplo fictício |
|---|---|---|---|---|---|
| `OPERACIONAL_GAP_OIT_OAT_ELEVADO` | `critical` | Pressão transacional por diferença OAT-OIT | `OAT - OIT >= 200000` | Backlog crítico de limpeza transacional; risco imediato de degradação. | `OIT=1000`, `OAT=260500` (gap 259500). |
| `OPERACIONAL_GAP_OIT_OAT_ACIMA_DO_ESPERADO` | `high` | Pressão transacional acima do esperado | `OAT - OIT >= 50000` | Pressão transacional alta, acima da faixa saudável. | `OIT=50000`, `OAT=125500` (gap 75500). |
| `OPERACIONAL_GAP_OIT_OAT_MODERADO` | `medium` | Pressão transacional moderada | `OAT - OIT >= 10000` | Sinal inicial de retenção, ainda controlável com ação rápida. | `OIT=880000`, `OAT=893500` (gap 13500). |
| `OPERACIONAL_GAP_OAT_OST_ELEVADO` | `high` | Backlog snapshot elevado por diferença OST-OAT | `OST - OAT >= 200000` | Leitores snapshot longos seguram versões antigas por muito tempo. | `OAT=320000`, `OST=540500` (gap 220500). |
| `OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO` | `medium` | Backlog snapshot acima do esperado | `OST - OAT >= 50000` | Retenção snapshot acima da linha de base operacional. | `OAT=700000`, `OST=761000` (gap 61000). |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA_CRITICA` | `critical` | Transação ativa longa crítica | idade >= `720` minutos | Há transação ativa aberta há muitas horas. | Sessão de conciliação aberta há 13h sem commit/rollback. |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA` | `high` | Transação ativa longa | idade >= `120` minutos | Transação ativa longa com risco relevante para retenção/locks. | Processo de integração com transação aberta há 2h40. |
| `OPERACIONAL_TRANSACAO_ATIVA_ACIMA_DO_ESPERADO` | `medium` | Duração acima do esperado | idade >= `30` minutos | Tempo de transação acima do padrão esperado para rotina. | Job de atualização com transação ativa há 45 min. |

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
