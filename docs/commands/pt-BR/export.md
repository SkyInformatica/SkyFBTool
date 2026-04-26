# Comando `export`

## O que faz
Exporta dados de tabela Firebird para script SQL em modo streaming.

## Como usar
```powershell
SkyFBTool export --database CAMINHO.fdb --table TABELA [opções]
```

## Todas as opções
- `--database`: caminho do banco.
- `--table`: tabela de origem.
- `--target-table`: nome da tabela destino no SQL gerado.
- `--output`: arquivo ou diretório de saída.
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--charset`: charset da conexão/saída.
- `--filter`: filtro simples (com prefixo `WHERE` opcional).
- `--filter-file`: filtro simples em arquivo.
- `--query-file`: `SELECT` completo em arquivo (modo avançado).
- `--blob-format`: `Hex` ou `Base64`.
- `--insert-mode`: `insert` (padrão) ou `upsert` (`UPDATE OR INSERT ... MATCHING`).
- `--commit-every`: gera `COMMIT` a cada N linhas.
- `--progress-every`: intervalo de progresso.
- `--split-size-mb`: tamanho de divisão do arquivo em MB (`0` desativa divisão).
- `--legacy-win1252`: força comportamento WIN1252 para bases legadas com `CHARSET NONE`.
- `--sanitize-text`: sanitiza textos antes de escrever no SQL.
- `--escape-newlines`: escapa quebras de linha em campos de texto.
- `--continue-on-error`: continua exportando após erro de escrita de linha.

## Regras
- Não combinar `--query-file` com `--filter` ou `--filter-file`.
- Em `--insert-mode upsert`, as colunas da PK precisam estar disponíveis para `MATCHING`.

## Exemplos
```powershell
SkyFBTool export --database "C:\dados\erp.fdb" --table CLIENTES --output "C:\exports\"
SkyFBTool export --database "C:\dados\erp.fdb" --table PEDIDOS --filter "STATUS = 'A'" --output "C:\exports\pedidos.sql"
SkyFBTool export --database "C:\dados\erp.fdb" --table ITENS --query-file ".\sql\itens.sql" --split-size-mb 200 --output "C:\exports\"
SkyFBTool export --database "C:\dados\erp.fdb" --table CLIENTES --insert-mode upsert --escape-newlines --output "C:\exports\clientes_upsert.sql"
```
