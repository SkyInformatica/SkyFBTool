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

## 9) Qualidade de Codigo e Manutenibilidade (Complementar)

Esta secao adiciona regras explicitas para evitar problemas recorrentes observados em geracoes de codigo por IA, como duplicacao, acoplamento excessivo e baixa legibilidade.

### 9.1) Evitar Duplicacao (DRY)
- Antes de criar nova logica, SEMPRE verificar se ja existe implementacao similar no projeto.
- Nao duplicar:
    - serializacao de valores SQL;
    - manipulacao de charset;
    - logica de parsing de comandos;
    - tratamento de erros.
- Se houver duplicacao:
    - extrair metodo ou classe reutilizavel;
    - manter responsabilidade clara.

### 9.2) Responsabilidade Unica (SRP)
- Cada classe deve ter um unico motivo para mudar.
- Evitar classes que:
    - fazem parsing + execucao + log ao mesmo tempo;
    - misturam regra de negocio com IO (console/arquivo).
- Separar claramente:
    - parsing de argumentos;
    - execucao de regra;
    - escrita de saida/log.
- Em CLI, manter tambem separadas as responsabilidades de:
    - parsing;
    - validacao;
    - resolucao de batch/wildcard;
    - ajuda/saida do usuario;
    - impressao de resultados.

### 9.3) Tamanho e Complexidade
- Evitar metodos longos (> ~50 linhas).
- Evitar blocos com multiplos niveis de `if/else`.
- Quando a logica crescer:
    - extrair funcoes menores;
    - usar nomes descritivos.

### 9.4) Nomes e Legibilidade
- Usar nomes explicitos (evitar abreviacoes desnecessarias).
- Nome deve refletir intencao, nao implementacao.
- Evitar comentarios desnecessarios - o codigo deve ser autoexplicativo.

### 9.5) Tratamento de Erros (padronizacao)
- Nunca ignorar excecoes silenciosamente.
- Sempre incluir contexto:
    - comando SQL;
    - linha aproximada;
    - arquivo.
- Evitar `catch (Exception)` generico sem rethrow ou log estruturado.

### 9.6) Consistencia Arquitetural
- Reutilizar estruturas existentes antes de criar novas:
    - `ExportadorTabelaFirebird`
    - `ImportadorSql`
    - `ExecutorSql`
- Nao criar novos "servicos paralelos" que duplicam responsabilidade.
- Manter padrao atual de organizacao por pasta.
- Se surgir logica comum de texto/idioma, batch pattern ou ajuda, centralizar em `Cli/Common` antes de duplicar nos comandos.

### 9.7) Dependencias
- Nao adicionar bibliotecas externas sem necessidade clara.
- Preferir bibliotecas nativas do .NET.
- Qualquer nova dependencia deve:
    - resolver problema real;
    - ser mencionada no README.

### 9.8) CLI e Experiencia do Usuario
- Mensagens devem ser:
    - curtas;
    - claras;
    - consistentes.
- Erros devem indicar como corrigir.
- Nao alterar comportamento de flags existentes sem necessidade.

### 9.9) Refatoracao Segura
- Refatorar apenas quando:
    - reduzir duplicacao;
    - melhorar clareza;
    - reduzir acoplamento.
- Evitar refatoracao junto com mudanca funcional grande.
- Preferir mudancas pequenas e revisaveis.

### 9.10) Checklist de Qualidade (Obrigatorio)
Antes de finalizar, validar:

1. Existe codigo duplicado introduzido?
2. Alguma funcao ficou muito longa ou complexa?
3. Foi reutilizado codigo existente corretamente?
4. O codigo esta facil de entender sem comentarios?
5. O tratamento de erro esta consistente?
6. Alguma responsabilidade ficou misturada indevidamente?
7. A mudanca manteve o padrao arquitetural do projeto?
8. A mudanca introduziu texto novo em PT-BR sem revisao de acentuacao?

Se qualquer resposta indicar problema de qualidade, ajustar antes de concluir.

### 10) Politica de Idioma (Ingles por padrao, PT-BR quando detectado)

Este projeto e internacional. O padrao de runtime e CLI e **inglês**. Quando `IdiomaSaidaDetector` identificar `pt-BR`, a mesma mensagem deve sair em portugues com acentuacao correta.
Documentacao interna pode estar em portugues.

#### 10.1) Onde usar INGLES
- Mensagens da CLI quando a cultura nao for `pt-BR`
- Logs operacionais
- README principal
- Nomes de comandos e flags

Exemplo:
- "Error executing command"
- "File not found"

#### 10.2) Onde usar PORTUGUES
- Mensagens da CLI quando `pt-BR` for detectado
- Nomes de classes, metodos, variaveis e arquivos
- Documentacao interna (ex: AGENTS.md, comentarios explicativos quando realmente necessarios)
- Materiais de apoio para desenvolvedores brasileiros

#### 10.3) Regra de Localizacao
- Toda mensagem nova voltada ao usuario deve ter variante em ingles e em portugues.
- A variante em ingles e o padrao de fallback.
- A variante em portugues deve usar acentuacao correta e linguagem natural.
- Quando a mensagem tiver suporte a idioma, usar o helper compartilhado de localizacao em vez de concatenar texto solto.

Exemplo:
- `CliText.Texto(idioma, "Invalid option.", "Opção inválida.")`

#### 10.4) Regra de Qualidade para Portugues
Quando portugues for utilizado:
- Sempre usar acentuacao correta
- Nao gerar texto sem acento (ex: "acao", "informacao")
- Evitar erros gramaticais basicos
- Manter linguagem clara e natural
- Texto em PT-BR nao pode ser gerado sem acento, cedilha ou com ortografia simplificada.
- Antes de finalizar, revisar strings novas com foco em palavras como `nao`, `opcao`, `padrao`, `relatorio`, `invalido`.

Exemplo:

Errado:
- "Erro na execucao do comando"
- "Arquivo nao encontrado"

Certo:
- "Erro na execução do comando"
- "Arquivo não encontrado"

#### 10.5) Consistencia (regra critica)
- Nunca misturar idiomas no mesmo contexto.
- Mensagens de runtime devem seguir o idioma da cultura detectada.
- Documentacao pode ser bilíngue, mas cada trecho precisa estar claro no idioma escolhido.
- Nao traduzir parcialmente mensagens ou nomes.

#### 10.6) Checklist de Idioma
Antes de finalizar, validar:

1. Codigo e CLI estao com ingles como padrao e PT-BR somente quando detectado?
2. Algum texto em portugues apareceu sem acentuacao correta?
3. Textos com suporte a idioma usam o helper compartilhado?
4. Ha mistura de idiomas no mesmo contexto?
5. A documentacao e a ajuda nao ficaram defasadas em relacao ao comportamento atual?

Se houver inconsistencias, corrigir antes de concluir.

### 11) Politica de Commits Git

- Mensagens de commit devem ser SEMPRE em ingles.
- Usar resumo objetivo no imperativo (ex: "Add DDL severity override config").
- Evitar mensagens vagas como "ajustes", "fixes", "update".
- Se houver mais de um commit, manter coerencia entre escopo e mensagem.
