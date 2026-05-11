[English](../en/operational-resilience.md) | Português (Brasil)

# Modelo de Resiliência Operacional

O SkyFBTool foi projetado para operações Firebird de longa duração e alto volume com padrões de execução resiliente.

## Objetivos de resiliência

- continuar com segurança sob falhas parciais quando a política permitir;
- evitar picos de memória em processamento grande de dados/scripts;
- preservar visibilidade de progresso e capacidade de diagnóstico;
- manter comportamento previsível sob carga.

## Mecanismos

1. Execução em streaming  
   Arquivos grandes e result sets são processados incrementalmente.

2. Comportamento com retry  
   Tratamento de falhas transientes reduz abortos desnecessários.

3. Controles de continuidade  
   `--continue-on-error` suporta continuidade em cenários operacionais em lote.

4. Pacing de commit e progresso  
   Intervalos configuráveis de commit/progresso melhoram controle operacional.

5. Particionamento de saída  
   `--split-size-mb` gera artefatos de exportação mais gerenciáveis.

6. Logging operacional  
   Logs por execução suportam diagnóstico e accountability.

## Prática recomendada

- usar fail-fast por padrão quando integridade de dados for prioridade;
- habilitar continue-on-error apenas com política operacional explícita;
- monitorar crescimento de logs e uso de disco em execuções grandes;
- manter comandos e saídas reproduzíveis para auditoria pós-execução.

## Documentos Relacionados

- Estratégia de testes e validações: [testing-and-validation-strategy.md](./testing-and-validation-strategy.md)
- Guia do comando `ddl-analyze`: [../../commands/pt-BR/ddl-analyze.md](../../commands/pt-BR/ddl-analyze.md)
