# Comando `import`

## O que faz
Executa um arquivo SQL no Firebird em modo streaming, com suporte a progresso e continuidade em erro.

## Como usar
```powershell
SkyFBTool import --database CAMINHO.fdb --input ARQUIVO.sql [opções]
```

## Opções principais
- `--database`: caminho do banco.
- `--input`: arquivo SQL de entrada.
- `--host`, `--port`, `--user`, `--password`: conexão.
- `--progress-every`: intervalo de progresso.
- `--continue-on-error`: continua após erro de comando.

## Exemplos
```powershell
SkyFBTool import --database "C:\dados\erp.fdb" --input "C:\exports\clientes.sql"
SkyFBTool import --database "C:\dados\erp.fdb" --input "C:\exports\pedidos.sql" --progress-every 5000 --continue-on-error
```

## Exemplo de saída
```text
Iniciando importação...
Linhas: 50.000 | Comandos: 49.990 | Velocidade: 2.100 cmd/s
Importação concluída.
Total de linhas processadas : 52.143
Total de comandos executados: 51.998
Tempo total de execução     : 00:00:24.318
Velocidade média            : 2.137,42 comandos/segundo
```
