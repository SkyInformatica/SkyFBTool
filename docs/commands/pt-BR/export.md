# Comando `export`

## O que faz
Exporta dados de tabela Firebird para script SQL em modo streaming.

## Como usar
```powershell
SkyFBTool export --database CAMINHO.fdb --table TABELA [opções]
```

## Todas as opções
- `--database`: arquivo/caminho do banco Firebird de origem.
- `--table`: nome da tabela de origem usado no `SELECT` padrão gerado pela ferramenta.
- `--target-table`: altera somente o nome da tabela escrito nos `INSERT`/`UPSERT`; útil quando exporta de uma tabela e importa em outra com schema compatível.
- `--output`: arquivo ou diretório de saída. Se for diretório, a ferramenta gera nome de arquivo com timestamp.
- `--host`: host do servidor Firebird (padrão: `localhost`).
- `--port`: porta do servidor Firebird (padrão: `3050`).
- `--user`: usuário Firebird (padrão: `sysdba`).
- `--password`: senha Firebird (padrão: `masterkey`).
- `--charset`: charset da conexão e do cabeçalho SQL (`SET NAMES`); use para preservar acentos/caracteres especiais.
- `--filter`: condição simples de `WHERE` aplicada na exportação da tabela; aceita prefixo `WHERE` opcional.
- `--filter-file`: mesmo comportamento de `--filter`, mas lendo a condição de um arquivo (melhor para condições longas).
- `--query-file`: `SELECT` completo personalizado (modo avançado). Use para joins, expressões, ordenação ou projeção explícita de colunas.
- `--blob-format`: formato de serialização de BLOB no SQL: `Hex` (padrão, mais portátil) ou `Base64` (normalmente menor).
- `--insert-mode`: `insert` (`INSERT` simples) ou `upsert` (`UPDATE OR INSERT ... MATCHING`); `upsert` é melhor para reimportação idempotente, mas depende de colunas de PK para `MATCHING`.
- `--commit-every`: escreve `COMMIT` a cada N linhas geradas; ajuda a reduzir transações longas na importação.
- `--progress-every`: exibe progresso no console a cada N linhas; não altera o SQL gerado, só observabilidade.
- `--split-size-mb`: divide a saída em múltiplos arquivos ao atingir o limite de tamanho; cada parte mantém cabeçalho SQL. Use `0` para desativar.
- `--legacy-win1252`: força estratégia legada WIN1252 para bases/arquivos `CHARSET NONE` com comportamento antigo de encoding.
- `--sanitize-text`: normaliza caracteres de controle problemáticos em campos texto para reduzir falhas de import/parser.
- `--escape-newlines`: transforma quebras de linha em sequências escapadas, melhorando legibilidade em linha única e estabilidade do parser.
- `--continue-on-error`: continua exportando após erros de serialização/escrita de linha, registrando falhas sem abortar a execução inteira.

## Regras
- Modo de entrada e composição da consulta
  - Não combinar `--query-file` com `--filter` ou `--filter-file`.
  - Use `--table` como modo padrão (exportação simples de tabela); use `--query-file` apenas quando precisar de controle total do SQL.
  - `--filter` e `--filter-file` são para predicados simples; eles são anexados à consulta gerada da tabela.

- Consistência entre origem e SQL gerado
  - Se usar `--target-table`, garanta que o schema da tabela de destino é compatível com colunas/ordem exportadas.
  - Em `--insert-mode upsert`, colunas da PK (ou chave de matching) precisam existir na projeção exportada para `MATCHING`.
  - Se customizar a projeção no `--query-file`, mantenha semântica de colunas compatível com o alvo da importação.

- Encoding e segurança de texto
  - Prefira `--charset` explícito para evitar ambiguidade de conversão de caracteres.
  - Use `--legacy-win1252` somente em cenários legados `CHARSET NONE` onde a decodificação padrão é comprovadamente incorreta.
  - Use `--sanitize-text` e/ou `--escape-newlines` quando parser/importador de destino apresentar problemas com caracteres de controle ou literais multilinha.

- Comportamento operacional em exportações grandes
  - Use `--split-size-mb` em saídas grandes para evitar arquivos únicos muito pesados.
  - Use `--progress-every` para acompanhar execução longa.
  - Use `--continue-on-error` apenas quando exportação parcial for aceitável e as falhas por linha forem revisadas depois.
  - `--commit-every` afeta os limites transacionais do script gerado durante a importação, não a consistência de leitura da origem.

## Exemplos
```powershell
SkyFBTool export --database "C:\dados\erp.fdb" --table CLIENTES --output "C:\exports\"
SkyFBTool export --database "C:\dados\erp.fdb" --table PEDIDOS --filter "STATUS = 'A'" --output "C:\exports\pedidos.sql"
SkyFBTool export --database "C:\dados\erp.fdb" --table ITENS --query-file ".\sql\itens.sql" --split-size-mb 200 --output "C:\exports\"
SkyFBTool export --database "C:\dados\erp.fdb" --table CLIENTES --insert-mode upsert --escape-newlines --output "C:\exports\clientes_upsert.sql"
```
