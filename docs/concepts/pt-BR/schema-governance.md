[English](../en/schema-governance.md) | Português (Brasil)

# Modelo de Governança de Schema

O SkyFBTool apoia governança de schema com artefatos reproduzíveis, visibilidade de drift e ciclos de revisão humana.

## Princípios de governança

- artefatos primeiro: gerar snapshots imutáveis antes de mudanças;
- visibilidade de drift: comparar estrutura de origem e alvo de forma explícita;
- promoção em etapas: revisar e aplicar em ambientes controlados;
- loop de verificação: validar convergência após aplicação.

## Blocos principais

1. `ddl-extract`  
   Gera snapshots `.sql` e `.schema.json` normalizados.

2. `ddl-diff`  
   Detecta diferenças estruturais e produz saídas SQL/JSON/HTML para revisão.

3. `ddl-analyze`  
   Classifica risco estrutural antes ou depois de mudanças planejadas.

## Revisão e aprovação humana

- revisar SQL gerado em homologação;
- validar ordem de dependências e estratégia de rollback;
- classificar achados por severidade e impacto de negócio;
- promover apenas após aprovação explícita.

## Rotina anti-drift

1. Extrair snapshots de origem/alvo.
2. Executar `ddl-diff`.
3. Revisar e aplicar SQL aprovado.
4. Reexecutar `ddl-diff` até convergência.
5. Manter artefatos para rastreabilidade.

## Documentos Relacionados

- Guia do comando `ddl-diff`: [../../commands/pt-BR/ddl-diff.md](../../commands/pt-BR/ddl-diff.md)
- Guia do comando `ddl-extract`: [../../commands/pt-BR/ddl-extract.md](../../commands/pt-BR/ddl-extract.md)
