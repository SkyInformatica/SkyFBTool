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

| Código | Severidade padrão | O que valida | Critério usado hoje | Explicação prática |
|---|---|---|---|---|
| `TABELA_SEM_COLUNAS` | `critical` | Tabela sem colunas | `tabela.Colunas.Count == 0` | A tabela existe no schema, mas não possui colunas válidas. Ex.: `CREATE TABLE LOG_AUDITORIA ();` |
| `COLUNA_DUPLICADA` | `critical` | Coluna repetida na mesma tabela | Agrupamento por nome de coluna (case-insensitive), contagem > 1 | A mesma coluna foi declarada mais de uma vez na tabela. Ex.: `CLIENTES(ID INT, NOME VARCHAR(60), NOME VARCHAR(80))`. |
| `TIPO_DESCONHECIDO` | `high` | Tipo SQL não mapeado | `TipoSql` começa com `TYPE_` | O tipo da coluna não foi reconhecido/mapeado para Firebird. Ex.: `TOTAL TYPE_99` em tabela de faturamento. |
| `TABELA_SEM_PK` | `high` | Tabela sem PK | `tabela.ChavePrimaria is null` | Tabela sem identificador primário confiável. Ex.: `PEDIDOS` com milhões de registros e sem `PRIMARY KEY`. |
| `PK_SEM_COLUNAS` | `critical` | PK sem lista de colunas | `ChavePrimaria.Colunas.Count == 0` | Constraint de PK existe, mas sem colunas válidas. Ex.: `PK_PEDIDOS` criada sem colunas associadas. |
| `PK_REFERENCIA_COLUNA_INEXISTENTE` | `critical` | PK aponta para coluna inexistente | Coluna da PK não encontrada no dicionário de colunas da tabela | A PK referencia campo inexistente. Ex.: PK usa `ID_PEDIDO`, mas a tabela tem apenas `COD_PEDIDO`. |
| `FK_SEM_COLUNAS` | `critical` | FK sem colunas locais e/ou de referência | `fk.Colunas.Count == 0` ou `fk.ColunasReferencia.Count == 0` | FK sem mapeamento relacional completo. Ex.: `FK_ITENS_PEDIDO` sem mapeamento de campos. |
| `FK_CARDINALIDADE_INVALIDA` | `critical` | FK com cardinalidade local/referência diferente | `fk.Colunas.Count != fk.ColunasReferencia.Count` | Quantidade de colunas local e referenciada não fecha. Ex.: FK local `(EMPRESA_ID, PEDIDO_ID)` para PK `(PEDIDO_ID)`. |
| `FK_COLUNA_LOCAL_INEXISTENTE` | `critical` | FK usa coluna local inexistente | Coluna local da FK não existe na tabela | A FK usa campo que não existe na tabela filha. Ex.: FK em `ITENS_PEDIDO` usa `FILIAL_ID`, mas coluna não existe. |
| `FK_TABELA_REFERENCIA_INEXISTENTE` | `critical` | FK referencia tabela inexistente | Tabela de referência não encontrada no mapa de tabelas | A FK aponta para tabela pai ausente no schema. Ex.: FK referencia `CLIENTE_MASTER`, mas só existe `CLIENTES`. |
| `FK_COLUNA_REFERENCIA_INEXISTENTE` | `critical` | FK referencia coluna inexistente na tabela alvo | Coluna de referência da FK não encontrada | A FK aponta para coluna ausente na tabela pai. Ex.: FK para `CLIENTES(ID_CLIENTE)`, mas a tabela pai tem `CLIENTE_ID`. |
| `FK_SEM_INDICE_COBERTURA` | `medium` | FK sem índice de cobertura local | Não há índice de suporte da própria FK e nenhum índice regular cobre o prefixo das colunas da FK | FK sem índice útil para join/validação relacional. Ex.: `PEDIDOS.CLIENTE_ID` com FK sem índice iniciando por `CLIENTE_ID`. |
| `INDICE_SEM_COLUNAS` | `high` | Índice sem colunas | `indice.Colunas.Count == 0` | Índice definido sem campos válidos. Ex.: índice `IDX_MOV` criado vazio por script incompleto. |
| `INDICE_COLUNA_INEXISTENTE` | `high` | Índice aponta para coluna inexistente | Coluna do índice não encontrada; itens de índice por expressão são ignorados | Índice inclui coluna ausente na tabela. Ex.: índice em `VENDAS(DATA_EMISSAO)` sem a coluna `DATA_EMISSAO`. Índices por expressão, como `UPPER(DESCRICAO)`, não são reportados como colunas inexistentes. |
| `INDICE_DUPLICADO` | `low` | Índices com mesma assinatura funcional | Mesma assinatura (`U/N`, `A/D`, lista ordenada de colunas) | Dois índices entregam a mesma função prática. Ex.: `IDX_A` e `IDX_B` em `(CLIENTE_ID, DATA)` ambos não únicos/ASC. |
| `INDICE_REDUNDANTE_PREFIXO` | `medium` | Índice possivelmente redundante por prefixo | Índice curto é prefixo de índice maior, mesma direção, ambos não únicos | Índice menor tende a ser redundante. Ex.: `(CLIENTE_ID)` e `(CLIENTE_ID, DATA)` na mesma direção. |
| `FK_DUPLICADA` | `low` | FKs com mesma assinatura funcional | Mesma assinatura (colunas locais, tabela/colunas referência, regras update/delete) | Há FKs diferentes repetindo a mesma regra relacional. Ex.: `FK_PED_CLIENTE_1` e `FK_PED_CLIENTE_2` com mesmos campos e regras. |
| `PROCEDURE_SEM_CORPO` | `critical` | Procedure sem corpo PSQL válido | Fonte da procedure vazia, sem `AS`/`BEGIN` ou apenas com comentários dentro de `BEGIN`/`END` | Metadado da procedure está incompleto ou inerte e pode quebrar geração ou reaplicação de DDL. Ex.: `SP_RECALCULAR_TOTAIS` extraída sem texto executável no corpo. |
| `FUNCTION_SEM_CORPO` | `critical` | Function sem corpo PSQL válido | Fonte da function vazia, sem `AS`/`BEGIN` ou apenas com comentários dentro de `BEGIN`/`END` | Metadado da function está incompleto ou inerte e pode quebrar geração ou reaplicação de DDL. Ex.: `FN_NORMALIZAR_NOME` extraída sem texto executável no corpo. |
| `TRIGGER_SEM_CORPO` | `critical` | Trigger sem corpo PSQL válido | Fonte do trigger vazia, sem `AS`/`BEGIN` ou apenas com comentários dentro de `BEGIN`/`END` | Metadado do trigger está incompleto ou inerte e pode quebrar geração ou reaplicação de DDL. Ex.: `TRG_PEDIDOS_BI` extraído sem texto executável no corpo. |
| `PROCEDURE_SOMENTE_SUSPEND` | `high` | Procedure com corpo PSQL inerte | Corpo contém apenas `SUSPEND` depois de ignorar comentários e separadores | A procedure existe e compila, mas retorna uma linha vazia/sem atribuições e sem lógica útil. Ex.: `BEGIN SUSPEND; END`. |
| `FUNCTION_SOMENTE_SUSPEND` | `high` | Function com corpo PSQL inerte | Corpo contém apenas `SUSPEND` depois de ignorar comentários e separadores | O metadado da function tem bloco executável, mas sem cálculo ou retorno útil. |
| `TRIGGER_SOMENTE_SUSPEND` | `high` | Trigger com corpo PSQL inerte | Corpo contém apenas `SUSPEND` depois de ignorar comentários e separadores | O trigger existe, mas não executa ação relevante. |

### Extracts SQL puros e procedures sem bloco PSQL

Quando o `ddl-analyze` lê metadados diretamente do banco, a classificação entre procedure de usuário e de sistema vem do catálogo do Firebird (`RDB$PROCEDURES.RDB$SYSTEM_FLAG`; `0` significa definida pelo usuário e `1` ou maior significa definida pelo sistema). Veja a referência oficial do catálogo `RDB$PROCEDURES` do Firebird: <https://www.firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref30/fblangref-appx04-procedures.html>.

Quando a entrada é um extract `.sql` avulso, essa flag de catálogo não está disponível. Algumas ferramentas de extração podem emitir uma definição final como `ALTER PROCEDURE ... AS`, sem bloco `BEGIN`/`END`, para rotinas que não expõem fonte PSQL no script. Nesse caso estrutural restrito, o `ddl-analyze` pula a validação de corpo PSQL dessa procedure em vez de reportar `PROCEDURE_SEM_CORPO` ou `PROCEDURE_SOMENTE_SUSPEND`.

Isso não é uma lista de nomes permitidos e não desativa a validação de procedures SQL em geral. Se o extract contiver `AS BEGIN ... END`, o corpo continua sendo analisado; um bloco contendo apenas comentários, separadores ou marcações inertes como `-- NOTHING` continua elegível para `PROCEDURE_SEM_CORPO`.

## Matriz de validações de compatibilidade de campos (`CAMPO_*`)

Esses códigos são gerados pelo validador de compatibilidade de campos e aparecem na mesma lista de achados do `ddl-analyze`.

| Código | Severidade padrão | O que valida | Critério usado hoje | Explicação prática |
|---|---|---|---|---|
| `CAMPO_TIPO_VAZIO` | `critical` | Tipo SQL ausente/vazio | `TipoSql` nulo, vazio ou whitespace | Metadado da coluna/domínio sem tipo válido para geração segura. |
| `CAMPO_TAMANHO_EFETIVO_EXCEDIDO` | `critical` | Tamanho efetivo de `CHAR/VARCHAR` excede limite do Firebird | `(tamanho declarado * bytes por caractere) > 32765` | Campo textual pode ultrapassar o limite real em bytes conforme charset. |
| `CAMPO_PRECISAO_NUMERICA_INVALIDA` | `critical` | Precisão/escala inválida em `NUMERIC/DECIMAL` | Precisão fora de `1..38`, escala `< 0` ou escala `> precisão` | Definição numérica inválida para DDL consistente. |
| `CAMPO_PRECISAO_NUMERICA_INCOMPATIVEL` | `critical` | Precisão incompatível com versão alvo | Firebird `< 4` e precisão `> 18` | Precisão exige recursos de versão mais nova do Firebird. |
| `CAMPO_TIPO_INCOMPATIVEL_VERSAO` | `critical` | Tipo incompatível com versão alvo | Tipo requer major mínimo (ex.: `BOOLEAN`>=3; `DECFLOAT/INT128/TIME WITH TIME ZONE/TIMESTAMP WITH TIME ZONE`>=4) | Tipo pode ser válido em versão nova, mas incompatível no ambiente legado alvo. |

## Matriz de validações operacionais (`MON$`) - apenas `--database`

| Código | Severidade padrão | O que valida | Limite atual | Significado prático |
|---|---|---|---|---|
| `OPERACIONAL_GAP_OIT_OAT_ELEVADO` | `critical` | Pressão transacional por diferença OAT-OIT | `OAT - OIT >= 200000` | Backlog crítico de limpeza transacional; risco imediato de degradação. Ex.: `OIT=1000`, `OAT=260500` (gap 259500). |
| `OPERACIONAL_GAP_OIT_OAT_ACIMA_DO_ESPERADO` | `high` | Pressão transacional acima do esperado | `OAT - OIT >= 50000` | Pressão transacional alta, acima da faixa saudável. Ex.: `OIT=50000`, `OAT=125500` (gap 75500). |
| `OPERACIONAL_GAP_OIT_OAT_MODERADO` | `medium` | Pressão transacional moderada | `OAT - OIT >= 10000` | Sinal inicial de retenção, ainda controlável com ação rápida. Ex.: `OIT=880000`, `OAT=893500` (gap 13500). |
| `OPERACIONAL_GAP_OAT_OST_ELEVADO` | `high` | Backlog snapshot elevado por diferença OST-OAT | `OST - OAT >= 200000` | Leitores snapshot longos seguram versões antigas por muito tempo. Ex.: `OAT=320000`, `OST=540500` (gap 220500). |
| `OPERACIONAL_GAP_OAT_OST_ACIMA_DO_ESPERADO` | `medium` | Backlog snapshot acima do esperado | `OST - OAT >= 50000` | Retenção snapshot acima da linha de base operacional. Ex.: `OAT=700000`, `OST=761000` (gap 61000). |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA_CRITICA` | `critical` | Transação ativa longa crítica | idade >= `720` minutos | Há transação ativa aberta há muitas horas. Ex.: sessão de conciliação aberta há 13h sem commit/rollback. |
| `OPERACIONAL_TRANSACAO_ATIVA_LONGA` | `high` | Transação ativa longa | idade >= `120` minutos | Transação ativa longa com risco relevante para retenção/locks. Ex.: processo de integração com transação aberta há 2h40. |
| `OPERACIONAL_TRANSACAO_ATIVA_ACIMA_DO_ESPERADO` | `medium` | Duração acima do esperado | idade >= `30` minutos | Tempo de transação acima do padrão esperado para rotina. Ex.: job de atualização com transação ativa há 45 min. |

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
