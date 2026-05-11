[English](../en/risk-mitigation.md) | Português (Brasil)

# Modelo de Mitigação de Riscos

O SkyFBTool trata operações Firebird como fluxos de engenharia com risco operacional, não apenas como execução de comandos.

## Objetivos

- reduzir a probabilidade de incidentes operacionais;
- reduzir o raio de impacto quando incidentes ocorrem;
- aumentar previsibilidade antes de rollout;
- melhorar rastreabilidade para análise pós-incidente.

## Camadas principais de mitigação

1. Análise estrutural  
   O `ddl-analyze` avalia achados de schema por severidade e contexto de risco.

2. Sinais operacionais  
   No modo por banco, dados de `MON$` adicionam sinais de pressão operacional à análise estrutural.

3. Priorização  
   Os achados são ordenados por severidade/índice de risco para foco no maior impacto primeiro.

4. Execução controlada  
   Streaming, pacing de commit e opções resilientes reduzem risco em runtime.

5. Auditabilidade  
   Artefatos JSON/HTML e logs suportam revisão, aprovação de mudança e postmortem.

## Sequência típica orientada a risco

1. Extrair baseline (`ddl-extract`).
2. Comparar ambientes (`ddl-diff`).
3. Analisar risco (`ddl-analyze`).
4. Remediar com execução SQL em etapas.
5. Reexecutar análise para confirmar redução de risco.

## Notas de escopo

- Modo por arquivo foca em risco estrutural.
- Modo por banco combina risco estrutural e operacional.
- Override de severidade adapta política, mas deve ser versionado e revisado.

## Documentos Relacionados

- Guia do comando `ddl-analyze`: [../../commands/pt-BR/ddl-analyze.md](../../commands/pt-BR/ddl-analyze.md)
- Matriz de severidade e validações: [../../commands/pt-BR/ddl-analyze-severity-and-validations.md](../../commands/pt-BR/ddl-analyze-severity-and-validations.md)
