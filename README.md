# SkyFBTool

Ferramenta CLI para **exportação** e **importação** de dados no Firebird (2.5 / 3.0 / 4.0), desenvolvida em **.NET 8**, com foco em desempenho, segurança e compatibilidade com bancos de grande porte.

Ideal para:

- migrações entre ambientes  
- criação de tabelas espelho  
- auditoria e saneamento  
- backups lógicos  
- replicações offline  
- preparar dados para homologação/produção  

---

## 🚀 Recursos Principais

---

## 🔷 Exportação

- Exporta uma tabela Firebird para arquivo `.sql` com apenas comandos `INSERT`.
- Conversão de BLOBs para **Hex** (padrão) ou **Base64**.
- Conversão correta de NUMERIC (sem notação científica).
- Compatível com bases CHARSET NONE usando modo RAW Win1252.
- Sanitização opcional de texto.
- Escape opcional de quebras de linha.
- Commit periódico configurável:
  ```
  --commit-every 5000
  ```
- Cabeçalho SQL seguro:
  ```sql
  SET SQL DIALECT 3;
  SET NAMES <CHARSET>;
  ```
*(Sem COMMIT inicial — evita erro de “no transaction exists”)*

- Permite renomear a tabela destino:
  ```
  --alias NOVA_TABELA
  ```

- Exporta por filtragem com cláusula WHERE:
  ```
  --where "CAMPO = VALOR"
  ```

- Suporte a arquivos extremamente grandes (streaming).
- Log de erros em `erros_exportacao.log`.

---

## 🔵 Filtro WHERE na exportação

É possível exportar somente uma parte da tabela utilizando:

```
--where "CAMPO = VALOR"
```

Exemplo:

```
SkyFBTool export \
  --database C:\dados\cartorio.fdb \
  --table ENCAMINHALANCAMENTOSELOS \
  --where "NROLANCAMENTO = 12345" \
  --output parcial.sql
```

Isso gera internamente o SELECT:

```sql
SELECT * FROM ENCAMINHALANCAMENTOSELOS
WHERE NROLANCAMENTO = 12345
```

Você pode usar qualquer condição válida do Firebird:

```
--where "DATAUTILIZACAO >= '2024-01-01'"
--where "SITUACAO <> 'C'"
--where "VALORSELO > 100 AND COBRARSELO = 'S'"
--where "UPPER(NOMEUSUARIO) LIKE '%JOAO%'"
```

---

## 🔷 Importação

- Executa arquivo `.sql` **linha por linha** (streaming).
- Executa automaticamente:
  - `SET SQL DIALECT`
  - `SET NAMES`
  - `INSERT`
  - `COMMIT`
- Transação totalmente controlada pelo arquivo exportado.
- Aceita arquivos enormes (GBs).
- Suporte a `--continue-on-error`.
- Log de erros: `erros_importacao.log`
- Progresso configurável:
  ```
  --progresso-cada 1000
  ```

---

## ⚠️ Observação importante sobre o charset **WIN1252**

O Firebird suporta `WIN1252`, porém o **.NET 8** não possui encoding 1252 nativo.  
O driver oficial `FirebirdSql.Data.FirebirdClient` depende do encoding do .NET e pode gerar:

```
Invalid character set specified.
No data is available for encoding 1252.
```

### 🟦 `--force-win1252` (modo RAW)
Modo especial criado para bases **CHARSET NONE** com dados gravados originalmente em `WIN1252`.

Esse modo:

- Lê campos como bytes (sem decodificação do .NET)
- Reconstrói manualmente preservando os bytes originais
- Evita perda de acentuação em bases legadas

### ✔ Como usar corretamente:

Base CHARSET NONE com dados WIN1252:
```
--force-win1252
```

Base UTF8, ISO8859_1, ou convertida corretamente:
```
--charset UTF8
```

Não use `--force-win1252` em bancos Unicode.

---

## 🧭 Como Usar

---

## 🔷 1) Exportar dados

```
SkyFBTool export [opções]
```

### Principais opções

| Parâmetro | Descrição |
|----------|-----------|
| `--database` | Caminho do .fdb |
| `--table` | Nome da tabela |
| `--alias` | Nome alternativo nos INSERTs |
| `--output` | Arquivo SQL |
| `--charset` | WIN1252, ISO8859_1, UTF8, NONE |
| `--blob-format` | Hex (padrão) ou Base64 |
| `--commit-every` | Insere COMMIT a cada N linhas |
| `--force-win1252` | Modo RAW para bases NONE |
| `--sanitize-text` | Remove caracteres inválidos |
| `--escape-newlines` | Converte quebras de linha |
| `--where` | Condição WHERE |
| `--continue-on-error` | Não interrompe |
| `--progresso-cada` | Progresso |

### Exemplo:

```
SkyFBTool export \
  --database C:\db\cartorio.fdb \
  --table ENCAMINHALANCAMENTOSELOS \
  --charset WIN1252 \
  --alias ENCAMINHALANCAMENTOSELOS_BKP \
  --commit-every 5000 \
  --output selos.sql
```

### Início do arquivo gerado:

```sql
SET SQL DIALECT 3;
SET NAMES WIN1252;

INSERT INTO ENCAMINHALANCAMENTOSELOS_BKP (...) VALUES (...);
INSERT INTO ENCAMINHALANCAMENTOSELOS_BKP (...) VALUES (...);
COMMIT;
```

---

## 🔷 2) Importar dados

```
SkyFBTool import [opções]
```

### Principais opções

| Parâmetro | Descrição |
|----------|-----------|
| `--database` | Caminho do .fdb |
| `--input` | Arquivo SQL |
| `--host` | Servidor |
| `--port` | Porta (3050) |
| `--user` | sysdba |
| `--password` | masterkey |
| `--continue-on-error` | Continua em erro |
| `--progresso-cada` | Progresso |

### Exemplo:

```
SkyFBTool import \
  --database C:\db\cartorio.fdb \
  --input selos.sql \
  --continue-on-error
```

---

## 📁 Estrutura do Projeto

```
SkyFBTool/
│
├── Core/
│   ├── OpcoesExportacao.cs
│   ├── OpcoesImportacao.cs
│   ├── FormatoBlob.cs
│   ├── IDestinoArquivo.cs
│
├── Infra/
│   ├── FabricaConexaoFirebird.cs
│   ├── DestinoArquivo.cs
│   ├── ConversorHex.cs
│   ├── LeitorRawWin1252.cs
│   ├── SanitizadorTexto.cs
│
├── Services/
│   ├── Export/
│   │   ├── ExportadorTabelaFirebird.cs
│   │   ├── ConstrutorInsert.cs
│   │   ├── ConstrutorConsultaFirebird.cs
│   │   ├── LeitorLinhaFirebird.cs
│   │
│   ├── Import/
│       ├── ImportadorSql.cs
│       ├── ExecutorSql.cs
│
└── Program.cs
```

---

## ⚠️ Declaração de Isenção de Responsabilidade

O SkyFBTool é distribuído sob a licença **MIT**, sendo fornecido **"NO ESTADO EM QUE SE ENCONTRA"**, sem garantias.

Os autores não se responsabilizam por:

- perda de dados
- corrupção de bancos
- falhas de execução
- danos diretos ou indiretos
- uso incorreto
- impactos causados a terceiros

Recomenda-se **testar em homologação** antes de uso em produção.

---

## 📄 Licença

MIT — livre para uso pessoal, comercial e corporativo.
```
