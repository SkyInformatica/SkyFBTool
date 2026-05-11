# SkyFBTool

[English](./README.md) | Português (Brasil)

SkyFBTool é uma plataforma de engenharia operacional para Firebird, focada em execução resiliente, governança estrutural e mitigação de riscos em cenários reais de produção.  
Ela combina operações de dados em fluxo contínuo, fluxos de governança de esquema e análise de risco DDL para Firebird `2.5`, `3.0`, `4.0` e `5.0`, incluindo cenários legados de charset.

## Visão do Projeto

O SkyFBTool foi projetado para tornar operações Firebird mais previsíveis, auditáveis e seguras:

- engenharia preventiva em vez de correções reativas;
- sinais estruturais e operacionais no mesmo fluxo de decisão;
- artefatos reproduzíveis para revisão humana e implantação controlada;
- padrões de execução resiliente para operações de grande volume e longa duração.

## Por que SkyFBTool

| Pilar | Entrega |
|---|---|
| Engenharia operacional | Execução em fluxo contínuo, processamento com retentativa, commits controlados, visibilidade de progresso |
| Governança de esquema | Extração de instantâneos de schema, detecção de desvio estrutural, fluxos de diff, saídas para revisão |
| Mitigação de riscos | Achados por severidade, priorização por tabela, sinais operacionais via MON$ |
| Observabilidade estrutural | Priorização de risco, sinais operacionais e visão consolidada por tabela |
| Prontidão para legado | Firebird `2.5 -> 5.0`, comportamento seguro para conjunto de caracteres, compatibilidade com `CHARSET NONE` |

## Principais Capacidades

### Engenharia operacional resiliente
- processo resiliente de movimentação e reaplicação SQL em larga escala;
- analisador SQL com suporte a comentários, strings e `SET TERM`;
- controles de execução: `--continue-on-error`, cadência de commit, intervalo de progresso;
- divisão de saída para exportações grandes (`--split-size-mb`);
- registro operacional para diagnóstico e auditoria.

### Processo de Governança Estrutural

- `ddl-extract`: artefatos normalizados de esquema (`.sql` + `.schema.json`);
- `ddl-diff`: detecção de desvio estrutural com saídas SQL, JSON e HTML;
- fluxos de trabalho baseados em instantâneos de schema para promoção e sincronização controlada.

### Análise de risco estrutural

- `ddl-analyze`: análise de risco de esquema com severidade e priorização;
- índice de risco e priorização por tabela (`P0..P3`);
- verificações operacionais via `MON$` no modo por banco;
- análise opcional de prioridade por volume (estimativa ou contagem exata).

## Diferenciais

- foco em risco operacional real, não apenas execução de comandos;
- combina análise estrutural e operacional no mesmo fluxo;
- suporte prático para restrições legadas de Firebird e charset;
- saídas orientadas a auditoria e decisões de implantação em etapas;
- priorização de correção orientada por severidade para fluxo de trabalho de DBA.

## Limites intencionais

Filosofia de segurança operacional:

- SQL gerado nunca é executado automaticamente;
- revisão humana é etapa obrigatória nos fluxos de promoção;
- operações destrutivas devem ser explícitas;
- continue-on-error não é comportamento padrão;
- artefatos devem permanecer auditáveis.

## Casos de Uso Reais

| Cenário | Ferramentas |
|---|---|
| Recuperação/movimentação massiva de dados | `export` + `import` |
| Auditoria estrutural de risco | `ddl-analyze` |
| Detecção de desvio estrutural entre ambientes | `ddl-diff` |
| Linha de base de governança DDL | `ddl-extract` |
| Execução operacional de scripts | `exec-sql` |

## Casos críticos suportados

Cenários suportados:

- bases com dezenas de milhões de registros;
- operações de longa duração;
- ambientes Firebird legados;
- desvio estrutural entre ambientes;
- reversão/reexecução operacional;
- diagnóstico de integridade estrutural;
- esteiras de auditoria.

## Arquitetura Conceitual

```text
ddl-extract
    ↓
ddl-diff
    ↓
ddl-analyze
    ↓
revisão/aprovação
    ↓
exec-sql / import
    ↓
revalidação
```

Resumo rápido:
- `ddl-extract` cria o baseline estrutural do ambiente;
- `ddl-diff` identifica o desvio entre origem e alvo;
- `ddl-analyze` prioriza riscos antes da aplicação da mudança;
- validação/revisão humana atua como mecanismo de controle antes da execução;
- `exec-sql` ou `import` executa a mudança planejada;
- revalidação confirma convergência estrutural e redução de risco.

## Fluxos Recomendados

### 1) Fluxo de migração de dados (DBA/operação)
1. Execute `export` na tabela/consulta de origem.
2. Revise SQL gerado e parâmetros de divisão/conjunto de caracteres.
3. Execute `import` no destino com monitoramento de progresso/log.
4. Valide o resumo e os logs de execução.

### 2) Fluxo de promoção de esquema (DBA + desenvolvimento)
1. Rode `ddl-extract` na origem e no destino.
2. Rode `ddl-diff` para gerar artefatos SQL/JSON/HTML.
3. Revise SQL e relatório HTML em homologação.
4. Aplique o SQL aprovado e rode novo `ddl-diff` para confirmar convergência.

### 3) Fluxo de triagem de risco (DBA)
1. Execute `ddl-analyze` (preferencialmente com `--database`).
2. Comece pelas tabelas priorizadas no relatório HTML.
3. Trate `critical/high` primeiro, depois `medium`.
4. Mantenha `low` como fila de otimização após validação por plano/carga.

### 4) Fluxo de execução operacional
1. Execute scripts de manutenção com `exec-sql`/`import`.
2. Acompanhe progresso e logs por comando.
3. Use `--continue-on-error` apenas quando continuidade for requisito explícito.

## Mapa de Documentação

### Documentação por comando

#### Operações de dados
- `export`: [docs/commands/pt-BR/export.md](./docs/commands/pt-BR/export.md)
- `import`: [docs/commands/pt-BR/import.md](./docs/commands/pt-BR/import.md)
- `exec-sql`: [docs/commands/pt-BR/exec-sql.md](./docs/commands/pt-BR/exec-sql.md)

#### Engenharia de esquema
- `ddl-extract`: [docs/commands/pt-BR/ddl-extract.md](./docs/commands/pt-BR/ddl-extract.md)
- `ddl-diff`: [docs/commands/pt-BR/ddl-diff.md](./docs/commands/pt-BR/ddl-diff.md)
- `ddl-analyze`: [docs/commands/pt-BR/ddl-analyze.md](./docs/commands/pt-BR/ddl-analyze.md)

#### Critérios técnicos
- Matriz de severidade/validação do `ddl-analyze`: [docs/commands/pt-BR/ddl-analyze-severity-and-validations.md](./docs/commands/pt-BR/ddl-analyze-severity-and-validations.md)

### Documentação conceitual

- Modelo de mitigação de riscos: [docs/concepts/pt-BR/risk-mitigation.md](./docs/concepts/pt-BR/risk-mitigation.md)
- Modelo de governança de esquema: [docs/concepts/pt-BR/schema-governance.md](./docs/concepts/pt-BR/schema-governance.md)
- Modelo de resiliência operacional: [docs/concepts/pt-BR/operational-resilience.md](./docs/concepts/pt-BR/operational-resilience.md)
- Modelo de compatibilidade Firebird: [docs/concepts/pt-BR/firebird-compatibility.md](./docs/concepts/pt-BR/firebird-compatibility.md)
- Estratégia de testes e validações: [docs/concepts/pt-BR/testing-and-validation-strategy.md](./docs/concepts/pt-BR/testing-and-validation-strategy.md)

## Início Rápido

```powershell
SkyFBTool export [opções]
SkyFBTool import [opções]
SkyFBTool exec-sql [opções]
SkyFBTool ddl-extract [opções]
SkyFBTool ddl-diff [opções]
SkyFBTool ddl-analyze [opções]
```

## Referências Operacionais

- Changelog: [CHANGELOG.pt-BR.md](./CHANGELOG.pt-BR.md)
- Publicações: [GitHub Releases](https://github.com/SkyInformatica/SkyFBTool/releases)
- Padrão de documentação: [DOCS_STANDARD.md](./DOCS_STANDARD.md)
- Esquema de referência para sobrescrita de severidade: `docs/examples/ddl-severity.sample.json`
- Amostras reproduzíveis de análise: `docs/examples/ddl-analyze-sample*.{sql,json,html}`

## Testes

Modelo operacional de testes e validações: [docs/concepts/pt-BR/testing-and-validation-strategy.md](./docs/concepts/pt-BR/testing-and-validation-strategy.md)

```powershell
dotnet test SkyFBTool.Tests\SkyFBTool.Tests.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

Testes de integração:

```powershell
$env:SKYFBTOOL_TEST_RUN_INTEGRATION="true"
.\SkyFBTool.Tests\run-integration-tests.ps1
```

## Releases automatizadas

Este repositório inclui uma esteira de publicação em `.github/workflows/release.yml`.

- Disparo: envio de tag no formato `v*` (ex.: `v0.1.0`)
- Pipeline: restore, build, testes, publish (`win-x64`, `linux-x64`)
- Saída: GitHub Release com artefatos compilados (`.tar.gz`)

Exemplo de tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Dependências

- `Scriban` é usado para renderizar relatórios HTML do `ddl-analyze` a partir de templates.

## Isenção de Responsabilidade

O SkyFBTool é fornecido sob licença MIT, "NO ESTADO EM QUE SE ENCONTRA", sem garantias de qualquer natureza.

Os autores não se responsabilizam por:
- perda de dados
- corrupção de banco de dados
- falhas de execução
- danos diretos ou indiretos
- uso indevido
- impactos em terceiros

Sempre valide em ambiente de homologação antes do uso em produção.

## Licença

MIT
