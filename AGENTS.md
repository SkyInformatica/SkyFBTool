# Guia de Programacao Assistida por IA (SkyFBTool)

Este arquivo orienta assistentes de IA a fazer mudancas seguras e consistentes no projeto.

## 1) Contexto do Projeto
- Tipo: ferramenta CLI em .NET 8 para exportacao/importacao de SQL em Firebird.
- Entrada principal: `SkyFBTool/Program.cs`.
- Dominios centrais:
  - Exportacao: `SkyFBTool/Services/Export/`
  - Importacao: `SkyFBTool/Services/Import/`
  - Infra compartilhada: `SkyFBTool/Infra/`
  - Contratos/opcoes: `SkyFBTool/Core/`

## 2) Principios Obrigatorios para Alteracoes
- Preservar processamento em streaming:
  - nao carregar arquivo SQL inteiro em memoria;
  - nao materializar tabelas inteiras para exportacao.
- Manter compatibilidade com Firebird 2.5/3.0/4.0/5.0.
- Preservar comportamento de charset:
  - `SET NAMES` no export;
  - deteccao de `SET NAMES` no import;
  - suporte a `--force-win1252` para bases legadas.
- Evitar refatoracao ampla sem necessidade direta da tarefa.
- Em mudancas de comportamento, atualizar ajuda CLI e README.

## 3) Mapa Rapido de Responsabilidades
- `Program.cs`
  - parse manual de argumentos;
  - roteamento de comandos `export` e `import`.
- `ExportadorTabelaFirebird`
  - escreve cabecalho SQL;
  - executa SELECT;
  - gera INSERTs;
  - controla commits/progresso/log de erro.
- `ConstrutorInsert`
  - serializacao de tipos Firebird para SQL literal;
  - BLOB (Hex/Base64) e texto.
- `ImportadorSql`
  - parser streaming de SQL (comentarios, string, delimitador via `SET TERM`);
  - controle transacional e metricas;
  - desativacao/reativacao de indices.
- `ExecutorSql`
  - executa comandos individuais (incluindo COMMIT e SET).

## 4) Regras de Implementacao para IA
- Preferir alteracoes pequenas e localizadas por modulo.
- Sempre tratar erros com contexto suficiente para diagnostico (linha/comando/arquivo).
- Evitar concatenacao SQL nova sem necessidade real; quando inevitavel, validar entradas.
- Garantir que opcoes novas:
  - tenham valor padrao coerente;
  - sejam documentadas na ajuda e README;
  - sejam consideradas em export/import quando aplicavel.
- Para logs grandes, usar append em arquivo dedicado e mensagens curtas no console.

## 5) Riscos Tecnicos Ja Observados (nao ignorar)
- `--where` continua sendo SQL livre do Firebird; embora exista validacao de tokens perigosos, validacao sintatica completa depende do banco.
- Operacoes de importacao/exportacao em arquivos muito grandes exigem monitorar espaco em disco e tamanho de logs.
- Ao adicionar novos parametros CLI, manter paridade entre parser, ajuda (`Program.cs`) e `README.md`.

Quando a tarefa tocar nesses pontos, priorizar correcao com impacto minimo e teste de regressao.

## 6) Checklist Antes de Finalizar
1. Mudanca atende exatamente o pedido do usuario?
2. Fluxo continua em streaming?
3. Charset/encoding foi preservado?
4. Ajuda CLI e README precisam ser atualizados?
5. Logs de erro continuam uteis?
6. Existe risco de quebra em importacao/exportacao de arquivo grande?

## 7) Checklist de Validacao Minima
- Build local sem erros.
- Executar ao menos um fluxo alvo alterado:
  - export simples de tabela pequena;
  - import simples com `SET NAMES` e `COMMIT`.
- Validar que os arquivos de log de erro continuam sendo gerados quando esperado.

## 8) Escopo e Estilo
- Manter nomes e padrao em portugues, como no codigo atual.
- Evitar adicionar dependencias externas sem justificativa forte.
- Evitar comentarios no codigo; priorizar codigo autoexplicativo.
