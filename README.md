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

##

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
##

## Em sistemas distribuídos e de missão crítica, falhas vão acontecer, o diferencial é como a arquitetura reage a elas.

### No pipeline de conciliação financeira em .NET Core: A falha de persistência é interceptada pelo Polly v8.   
#### O pipeline executa 3 retentativas com backoff exponencial e jitter para absorver instabilidades transitórias.
#### Ao esgotar o limite, a mensagem é encaminhada de forma segura para a Dead Letter Queue (DLQ) do Azure Service Bus, liberando a fila e evitando perda de dados ou paradas no container.

<img width="1049" height="348" alt="image" src="https://github.com/user-attachments/assets/e91328eb-2c78-4638-ac4f-5808f46ce410" />

##

<img width="1241" height="512" alt="image" src="https://github.com/user-attachments/assets/5eca45a3-2afa-4c25-afb0-3b178f61def4" />

## Reprocessando o que tava preso no dlq

<img width="1067" height="338" alt="image" src="https://github.com/user-attachments/assets/710a3bbb-390e-475f-8158-44003375d4bf" />

## Observabilidade e Monitorização

<img width="1600" height="860" alt="image" src="https://github.com/user-attachments/assets/ac923a75-fcd1-46d1-a67c-f9e943bf9233" />


### A monitorização da resiliência do pipeline e do consumo assíncrono do Worker foi implementada com **OpenTelemetry**, exportando métricas canónicas no formato OpenMetrics para o **Prometheus** e centralizando a visualização em dashboards em tempo real no **Grafana**.

### Métricas Exportadas pelo Worker

| Métrica | Tipo | Descrição |
| :--- | :--- | :--- |
| `reconciliacao_lotes_sucesso_total` | Counter | Total de lotes processados e conciliados na base de dados com êxito. |
| `reconciliacao_retentativas_total` | Counter | Total de retentativas executadas pelas políticas de resiliência do Polly. |
| `reconciliacao_mensagens_dlq_total` | Counter | Total de mensagens irrecuperáveis descartadas para a Dead Letter Queue (DLQ). |

---

### Testes de Resiliência e Dead Letter Queue (DLQ)

Simulação de falha temporária e esgotamento da política de retry via Polly, culminando no descarte e posterior recuperação da mensagem presa na DLQ.

#### Reprocessamento da Dead Letter Queue
<img width="1067" height="338" alt="Reprocessando mensagens na DLQ" src="https://github.com/user-attachments/assets/710a3bbb-390e-475f-8158-44003375d4bf" />

---

### Coleta e Série Temporal no Prometheus

Validação das séries temporais geradas e raspadas diretamente no endpoint `/metrics` do `financeiro-worker`:

#### 1. Lotes Conciliados com Sucesso (`reconciliacao_lotes_sucesso_total`)
<img width="1600" height="796" alt="Métrica de lotes conciliados com sucesso no Prometheus" src="https://github.com/user-attachments/assets/3e1f7c57-b185-4802-b819-5cb4c97c104e" />

#### 2. Mensagens Encaminhadas para a DLQ (`reconciliacao_mensagens_dlq_total`)
<img width="1600" height="803" alt="Métrica de mensagens na DLQ no Prometheus" src="https://github.com/user-attachments/assets/188acd70-c950-481e-a1bf-fc9b287f4c35" />

---

### Dashboard Unificado no Grafana

Visão executiva em tempo real com thresholds configurados para destacar anomalias operacionais (alertando falhas em vermelho e exibindo o volume de retentativas executadas pelo Polly):

<img width="1600" height="831" alt="Dashboard operacional no Grafana" src="https://github.com/user-attachments/assets/69ed2b34-4677-43fa-810f-1fd4f4b90774" />

