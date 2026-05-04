# TODO

Backlog curto e prático para próximos ciclos de melhoria.

## DDL

- `ddl-analyze`: sinalizar `PROCEDURE`, `FUNCTION` e `TRIGGER` sem corpo como achado de análise.
- `ddl-diff`: validar `DOMAIN` por flag; o comportamento padrão é ignorar `DOMAIN` na comparação.
- `ddl-extract`: revisar eventual diferença de metadata em Firebird 2.5 a 5.0 para `FUNCTION` e `TRIGGER` em bases legadas.

## Documentação

- Atualizar exemplos quando houver mudança de assinatura ou de `SET TERM` em objetos PSQL.
- Manter README e docs de comando alinhados com o comportamento real do snapshot gerado.
