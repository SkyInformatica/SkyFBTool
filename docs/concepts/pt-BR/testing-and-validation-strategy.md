[English](../en/testing-and-validation-strategy.md)

# Estratégia de Testes e Validações

## Propósito

Os testes do SkyFBTool foram projetados para proteger previsibilidade operacional, integridade de dados e execução resiliente em fluxos reais de Firebird.  
O objetivo não é apenas cobertura de código. O objetivo é reduzir regressões silenciosas em operações críticas de banco de dados.

Em termos práticos, este documento responde: **até onde podemos confiar operacionalmente no comportamento do SkyFBTool**.  
A resposta é: existe uma base sólida de confiança para fluxos críticos já cobertos, com limites explícitos que continuam sob governança humana.

## Modelo de Validação

O projeto usa validação em camadas:

1. Testes unitários e de serviços para comportamento determinístico e regras de validação.
2. Testes de CLI e comandos para parsing de parâmetros, aliases e estabilidade do contrato de uso.
3. Testes de integração com bancos Firebird reais para execução ponta a ponta.
4. Artefatos reproduzíveis de relatório (`docs/examples`) para verificação visual e consistência operacional.

Essa combinação fornece feedback rápido de regressão e confiança operacional realista.

## Categorias de Teste e o Que Validam

## 1) Testes Unitários e de Serviço

Foco:
- comportamento de parser e serialização;
- regras de comparação e análise DDL;
- normalização de severidade, validações de compatibilidade e agregação de risco.

Garantias operacionais:
- saídas determinísticas para entradas equivalentes;
- estabilidade das regras de achados DDL;
- detecção antecipada de regressões lógicas antes da integração.

## 2) Testes de Contrato de CLI e Comandos

Foco:
- parsing de opções e aliases;
- validação de argumentos obrigatórios/inválidos;
- comportamento de wildcard em lote e roteamento de comando.

Garantias operacionais:
- contrato de linha de comando previsível;
- redução do risco de quebrar scripts de automação e pipelines de CI;
- orientação consistente ao usuário em erros operacionais.

## 3) Testes de Integração (Firebird Real)

Foco:
- fluxos completos de export/import e DDL contra banco real;
- geração e replay real de SQL;
- comportamento de charset em UTF8, WIN1252 e cenários legados.

Garantias operacionais:
- execução ponta a ponta funcional em ambientes realistas;
- preservação de comportamento esperado em movimentação e replay;
- confiança de compatibilidade para instalações Firebird legadas e modernas.

## Cenários Críticos Cobertos Hoje

A cobertura atual de integração valida:

- roundtrip de export/import com dados reais em UTF8 e WIN1252;
- comportamento relacionado a legado (`NONE` + WIN1252 forçado) para compatibilidade de saída;
- tratamento de acentuação e textos em português em caminhos operacionais;
- comportamento resiliente sob falhas intermitentes controladas;
- fluxos com `continue-on-error` e geração explícita de logs;
- cenários de grande volume em import/export com expectativas de progresso e estabilidade;
- geração de artefatos SQL/snapshot/auditoria no `ddl-extract`;
- geração de relatório `ddl-diff` com estilo de impressão e indicadores visuais;
- comportamento do resumo em lote do `ddl-analyze` para bases sem achados (`none` / não aplicável).

## Garantias Operacionais da Suíte

Hoje, a suíte oferece garantias fortes em:

- **Previsibilidade:** estabilidade de comportamento de comandos e relatórios para entradas repetíveis.
- **Integridade:** consistência dos caminhos de replay de dados e artefatos de schema após mudanças.
- **Resiliência:** caminhos de falha (retry, continue-on-error, logs) explícitos e testáveis.
- **Compatibilidade:** validação contínua de caminhos Firebird em padrões relevantes de charset/legado.
- **Controle de Regressão:** detecção de mudanças críticas de comportamento antes de publicar release.

Essas garantias refletem um modelo de engenharia preventiva: o comportamento é validado antes da produção, os cenários de falha são exercitados, e os artefatos operacionais são tratados como parte do contrato do sistema.

## Limites e Validação Humana Necessária

Alguns pontos ainda dependem de revisão humana, homologação ou ensaio controlado:

- comportamento de performance sob variação real de infraestrutura (I/O, rede, contenção de storage);
- correção semântica de DDL gerado frente a políticas específicas de governança de cada ambiente;
- aprovação humana final para planos de aplicação SQL destrutivos;
- legibilidade visual e impressão em fluxos específicos de PDF da organização;
- decisões operacionais de aceitação/priorização de risco dos achados DDL.

Teste reduz risco, mas não substitui governança operacional.

Esse limite não é fragilidade do processo; é uma decisão de engenharia para ambientes críticos: automação valida repetibilidade e segurança técnica, enquanto homologação valida contexto de negócio e risco residual.

## Linha de Base de Validação para Release

Antes de liberar versão, a linha de base prática mínima é:

1. build limpo e execução de testes automatizados;
2. execução do script de integração com conectividade Firebird real;
3. verificação dos artefatos gerados em `docs/examples` quando houver mudança de relatório;
4. alinhamento do changelog com o comportamento entregue.

Isso mantém releases auditáveis, reproduzíveis e confiáveis no uso operacional.

## Nível de Confiança Operacional

Com a estratégia atual, o SkyFBTool apresenta:

- maturidade de engenharia em fluxos críticos de dados e DDL;
- foco operacional real em previsibilidade e resiliência;
- mitigação ativa de regressões silenciosas;
- estabilidade de comportamento sustentada por testes automatizados e validação de integração.

Em resumo: **a ferramenta é operacionalmente confiável dentro dos cenários cobertos, com fronteiras claras para validação humana nos pontos de maior impacto**.

## Documentos Relacionados

- Modelo de resiliência operacional: [operational-resilience.md](./operational-resilience.md)
- Modelo de mitigação de riscos: [risk-mitigation.md](./risk-mitigation.md)
- Modelo de governança de schema: [schema-governance.md](./schema-governance.md)
- Exemplos reproduzíveis de relatórios: [../../examples/](../../examples/)
- Guia do comando `ddl-analyze`: [../../commands/pt-BR/ddl-analyze.md](../../commands/pt-BR/ddl-analyze.md)
- Guia do comando `ddl-diff`: [../../commands/pt-BR/ddl-diff.md](../../commands/pt-BR/ddl-diff.md)
