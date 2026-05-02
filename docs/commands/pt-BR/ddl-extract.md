# Comando `ddl-extract`

## O que faz
Extrai metadados de schema Firebird e gera duas saídas sincronizadas:
- script DDL legível (`.sql`) para inspeção humana
- snapshot normalizado (`.schema.json`) para fluxos automatizados de diff/análise

`ddl-extract` é a etapa canônica antes de `ddl-diff` e `ddl-analyze` quando você quer artefatos reprodutíveis de metadados.

## Quando usar
- DBA: capturar baseline do schema antes de manutenção ou migração.
- Desenvolvedor: gerar snapshots versionáveis de schema para revisão e comparação em CI.

## Como usar
```powershell
SkyFBTool ddl-extract --database CAMINHO.fdb --output PREFIXO [opções]
```

## Todas as opções
- `--database`: banco Firebird de origem.
- `--output`: prefixo/arquivo base/diretório de saída.
  - Prefixo/arquivo base: gera `<prefixo>.sql` e `<prefixo>.schema.json`.
  - Diretório: a ferramenta gera nome base com timestamp dentro do diretório.
- `--host`: host do servidor (padrão: `localhost`).
- `--port`: porta do servidor (padrão: `3050`).
- `--user`: usuário (padrão: `sysdba`).
- `--password`: senha (padrão: `masterkey`).
- `--charset`: charset opcional de conexão; use quando metadados/textos do banco exigirem tratamento explícito.

## Regras e orientação operacional
- Use credenciais/configuração de conexão estáveis entre extrações de origem e alvo quando o objetivo for `ddl-diff` determinístico.
- Mantenha os `.schema.json` extraídos versionados para rastrear evolução de schema ao longo do tempo.
- Prefira extrair com banco em estado consistente (fora de janela de migração ativa) para evitar diffs transitórios.
- Use convenção explícita de nomes de saída (por exemplo, ambiente/data) para facilitar auditoria.

## Categorias de falha da extração
Quando a extração falha, a CLI classifica a causa raiz em:
- `incompatible_ods`
- `permission_denied`
- `database_file_access`
- `metadata_query_failure`
- `connection_failure`
- `unknown`

## Fluxo prático
1. Execute `ddl-extract` no banco de origem.
2. Execute `ddl-extract` no banco de destino.
3. Use os dois `.schema.json` no `ddl-diff`.
4. Opcionalmente execute `ddl-analyze` no snapshot ou no modo direto por banco.

## Exemplos
```powershell
SkyFBTool ddl-extract --database "C:\dados\origem.fdb" --output "C:\ddl\origem"
SkyFBTool ddl-extract --database "C:\dados\alvo.fdb" --output "C:\ddl\alvo"
SkyFBTool ddl-extract --database "C:\dados\prod.fdb" --charset WIN1252 --output "C:\ddl\prod_2026_05_01"
```
