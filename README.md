# Pipeline de Conciliação Financeira (.NET 8)

Pipeline assíncrono de conciliação financeira de elevado desempenho construído com **.NET 8**, processamento em lote via **Azure Service Bus Emulator**, persistência e TVPs no **SQL Server 2022**, idempotência com **Redis** e armazenamento de ficheiros com **Azurite**.

---

## 🛠️ Pré-requisitos

* [Git](https://git-scm.com/)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) (com suporte WSL2 ativado no Windows)

---

## 🚀 Como Executar o Projeto

### 1. Clonar o repositório
```bash
git clone [https://github.com/tiowick/reconciliacao-financeira-pipeline.git](https://github.com/tiowick/reconciliacao-financeira-pipeline.git)
cd reconciliacao-financeira-pipeline
```

### 2. Iniciar toda a infraestrutura e aplicações
Execute na raiz da solução para compilar as imagens e iniciar os contentores em segundo plano:
```bash
docker compose up --build -d
```

### 3. Validar se os serviços estão ativos
```bash
docker compose ps
```

Os 6 contentores devem apresentar o estado `Up` ou `running`:
* `mssql-server` (Porta `1433`)
* `redis-cache` (Porta `6379`)
* `azurite` (Portas `10000`, `10001`, `10002`)
* `sb-emulator` (Portas `5672`, `5300`)
* `financeiro-api` (Porta `5261`)
* `financeiro-worker` (Background Service)

---
## Docker Desktop

<img width="1600" height="857" alt="image" src="https://github.com/user-attachments/assets/fd67ef6f-20bc-46f6-9cda-d20a79ace6d8" />



## 🔍 Monitorização de Registos (Logs)

Acompanhe a atividade dos serviços em tempo real:
```bash
# Registos do Worker
docker logs -f financeiro-worker

# Registos da API
docker logs -f financeiro-api
```

---

## 📬 Testar a API

* **Endereço Base:** `http://localhost:5261`
* **Documentação Swagger:** `http://localhost:5261/swagger`

### Carregamento de Ficheiro (Upload CSV)
* **Método:** `POST`
* **Rota:** `http://localhost:5261/api/reconciliacao/upload`
* **Formato:** `form-data`
  * Chave: `arquivo` (Tipo: `File`)
  * Valor: Selecionar o ficheiro CSV (ex: `teste_.csv`)

---

## 🛑 Encerrar os Serviços

```bash
# Parar os contentores mantendo os volumes
docker compose down

# Parar e repor integralmente os dados dos volumes
docker compose down -v
```

## Execução direto no Postman

<img width="960" height="530" alt="image" src="https://github.com/user-attachments/assets/cf23ba95-3952-408a-8e2c-ba98e545e21e" />



## Verificar status

<img width="973" height="583" alt="image" src="https://github.com/user-attachments/assets/426a0518-3e95-4ff8-be36-e1acfcdd1cd1" />

## Divergências

```bash

{
    "protocoloId": "9c3077b7-fc1a-491b-a748-f8f9a82b69e2",
    "totalDivergencias": 7,
    "itens": [
        {
            "id": 11,
            "documentoCliente": "12345",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 100.00,
            "tipo": "CREDITO",
            "motivoRejeicao": "Documento invalido (deve possuir 11 ou 14 digitos)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 12,
            "documentoCliente": "9876543210987654",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 250.00,
            "tipo": "DEBITO",
            "motivoRejeicao": "Documento invalido (deve possuir 11 ou 14 digitos)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 13,
            "documentoCliente": "22233344455",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 0.00,
            "tipo": "CREDITO",
            "motivoRejeicao": "Valor da transacao invalido (<= 0)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 14,
            "documentoCliente": "66677788899",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": -50.00,
            "tipo": "DEBITO",
            "motivoRejeicao": "Valor da transacao invalido (<= 0)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 15,
            "documentoCliente": "12345678901",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 150.50,
            "tipo": "CREDITO",
            "motivoRejeicao": "Transacao duplicada no lote",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 16,
            "documentoCliente": "12345678901",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 150.50,
            "tipo": "CREDITO",
            "motivoRejeicao": "Transacao duplicada no lote",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 20,
            "documentoCliente": "11122233",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 67.80,
            "tipo": "DEBITO",
            "motivoRejeicao": "Documento invalido (deve possuir 11 ou 14 digitos)",
            "criadoEm": "2026-09-23T22:09:02.485"
        }
    ]
}

```
## Reprocessamento 

<img width="1045" height="720" alt="image" src="https://github.com/user-attachments/assets/fd7f38c9-708f-4032-b863-1606268b0b9d" />

## Verificando o retorno consolidado

<img width="959" height="613" alt="image" src="https://github.com/user-attachments/assets/91c90794-262f-4ade-924b-9e72a9a86035" />

## Verificando novamente as divergências : 

<img width="979" height="580" alt="image" src="https://github.com/user-attachments/assets/5517c6c4-8db6-411d-bba9-4c8befcbe518" />

```bash
{
    "protocoloId": "9c3077b7-fc1a-491b-a748-f8f9a82b69e2",
    "totalDivergencias": 6,
    "itens": [
        {
            "id": 11,
            "documentoCliente": "12345",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 100.00,
            "tipo": "CREDITO",
            "motivoRejeicao": "Documento invalido (deve possuir 11 ou 14 digitos)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 12,
            "documentoCliente": "9876543210987654",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 250.00,
            "tipo": "DEBITO",
            "motivoRejeicao": "Documento invalido (deve possuir 11 ou 14 digitos)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 13,
            "documentoCliente": "22233344455",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 0.00,
            "tipo": "CREDITO",
            "motivoRejeicao": "Valor da transacao invalido (<= 0)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 14,
            "documentoCliente": "66677788899",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": -50.00,
            "tipo": "DEBITO",
            "motivoRejeicao": "Valor da transacao invalido (<= 0)",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 16,
            "documentoCliente": "12345678901",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 150.50,
            "tipo": "CREDITO",
            "motivoRejeicao": "Transacao duplicada no lote",
            "criadoEm": "2026-09-23T22:09:02.485"
        },
        {
            "id": 20,
            "documentoCliente": "11122233",
            "dataTransacao": "2026-09-21T00:00:00",
            "valor": 67.80,
            "tipo": "DEBITO",
            "motivoRejeicao": "Documento invalido (deve possuir 11 ou 14 digitos)",
            "criadoEm": "2026-09-23T22:09:02.485"
        }
    ]
}

```

## Exatamente os 6 registos rejeitados com os respetivos motivos de erro preenchidos pelo motor T-SQL (MotivoRejeicao).

## ⚙️ Regras de Validação Aplicadas

O motor de conciliação executado na base de dados (`ssp_ConciliarTransacoesProtocolo`) valida e segrega as transações com base nas seguintes diretrizes:

* **Documento Inválido:** Rejeita registos cujo tamanho de `DocumentoCliente` seja diferente de 11 (CPF) ou 14 dígitos (CNPJ).
* **Valor Inválido:** Rejeita transações com valor inferior ou igual a zero (`Valor <= 0`).
* **Duplicidade no Lote:** Identifica e rejeita registos duplicados no mesmo protocolo com os mesmos dados (`DocumentoCliente`, `DataTransacao`, `Valor` e `Tipo`).
* **Conciliação Efetiva:** Promove automaticamente a `CONCILIADO` todas as transações que cumprem os critérios de integridade.

---

## 🛡️ Rastreabilidade e Auditoria

Todas as operações de ingestão e reprocessamento geram registos estruturados na tabela `dbo.LogsAuditoria`, garantindo observabilidade e auditoria de ponta a ponta:

```sql
SELECT DataHora, Operacao, Nivel, Origem, Mensagem 
FROM dbo.LogsAuditoria 
ORDER BY Id DESC;

```
