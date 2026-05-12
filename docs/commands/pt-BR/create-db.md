[English](../en/create-db.md)

# `create-db`

Cria um novo arquivo de banco Firebird.

## Objetivo

Use `create-db` para provisionar um banco com parâmetros operacionais explícitos (charset, page size, forced writes), com comportamento seguro por padrão.

## Opções

- `--database <caminho>`: caminho do arquivo `.fdb` de destino (obrigatório)
- `--host <servidor>`: host do Firebird (padrão: `localhost`)
- `--port <número>`: porta do Firebird (padrão: `3050`)
- `--user <nome>`: usuário (padrão: `sysdba`)
- `--password <valor>`: senha (padrão: `masterkey`)
- `--charset <nome>`: charset do banco (padrão: `UTF8`)
- `--page-size <número>`: tamanho de página em bytes (padrão: `8192`)
- `--forced-writes on|off`: modo de escrita forçada (padrão: `on`)
- `--overwrite`: recria o arquivo caso já exista
- `--ddl-file <caminho.sql>`: aplica script SQL logo após a criação do banco

## Comportamento e segurança

- Se o arquivo de destino já existir, o comando falha por padrão.
- `--overwrite` é obrigatório para recriar um arquivo existente.
- O diretório de destino é criado automaticamente quando necessário.
- Se `--ddl-file` for informado, a execução do script ocorre em modo fail-fast (`continue-on-error` desativado).

## Exemplo

```powershell
SkyFBTool create-db --database "C:\dados\novo_banco.fdb" --charset UTF8 --page-size 8192
SkyFBTool create-db --database "C:\dados\novo_banco.fdb" --ddl-file "C:\ddl\schema_extraido.sql"
```
