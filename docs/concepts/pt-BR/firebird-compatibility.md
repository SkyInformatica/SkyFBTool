[English](../en/firebird-compatibility.md) | Português (Brasil)

# Modelo de Compatibilidade Firebird

O SkyFBTool é orientado à compatibilidade operacional entre Firebird `2.5`, `3.0`, `4.0` e `5.0`.

## Prioridades de compatibilidade

- preservar comportamento em ambientes de versões mistas;
- manter workflows viáveis para bases legadas;
- tornar tratamento de charset explícito e revisável;
- expor achados sensíveis à versão em saídas de análise.

## Considerações de charset e encoding

- exportação inclui `SET NAMES`;
- importação detecta e respeita `SET NAMES`;
- cenários legados `CHARSET NONE` são suportados via `--legacy-win1252`;
- charset explícito é recomendado em migrações críticas.

## Notas para operação legada segura

- validar premissas de encoding antes de execuções grandes de import/export;
- tratar modo legado como exceção controlada, não baseline padrão;
- preferir scripts reproduzíveis e verificação em etapas para bases antigas.

## Validação estrutural sensível à versão

O `ddl-analyze` inclui validações de compatibilidade (por exemplo, tipo/precisão/versão) para evitar promoção de DDL não suportado.

## Guia prático

1. Definir baseline de versão Firebird por ambiente.
2. Extrair e analisar schema antes de promoção.
3. Corrigir achados incompatíveis antes do rollout.
4. Reexecutar validações após aplicação para confirmar estabilidade.
