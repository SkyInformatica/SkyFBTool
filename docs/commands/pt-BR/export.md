# Comando `export`

## O que faz
Exporta dados de uma tabela Firebird para script SQL (`INSERT`s), com processamento em streaming.

## Como usar
```powershell
SkyFBTool export --database CAMINHO.fdb --table TABELA [opções]
```

## Opções principais
- `--database`: caminho do banco.
- `--table`: tabela de origem.
- `--output`: arquivo ou diretório de saída.
- `--filter` / `--filter-file`: filtro simples.
- `--query-file`: `SELECT` completo (não combinar com `--filter`).
- `--commit-every`: gera `COMMIT` a cada N linhas.
- `--split-size-mb`: divide arquivo em partes.
- `--blob-format`: `Hex` ou `Base64`.

## Exemplos
```powershell
SkyFBTool export --database "C:\dados\erp.fdb" --table CLIENTES --output "C:\exports\"
SkyFBTool export --database "C:\dados\erp.fdb" --table PEDIDOS --filter "STATUS = 'A'" --output "C:\exports\pedidos.sql"
SkyFBTool export --database "C:\dados\erp.fdb" --table ITENS --query-file ".\sql\itens.sql" --split-size-mb 200 --output "C:\exports\"
```

## Exemplo de saída
```text
Modo de consulta / Query mode: Simple/Simples (--table + --filter)
Divisão de arquivo ativa: 200 MB por arquivo.
Iniciando exportação...

Resumo da exportação
------------------------------------------------------------------------
Arquivos gerados : 2
[1] 199.8 MB  C:\exports\ITENS_20260425_101500_123.sql
[2]  12.4 MB  C:\exports\ITENS_20260425_101500_123_part002.sql
Arquivo final    : C:\exports\ITENS_20260425_101500_123_part002.sql
------------------------------------------------------------------------
```
