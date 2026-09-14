# FIAP Cloud Games - PaymentsAPI

Microsservico de pagamentos evoluido para a Fase 3 do Tech Challenge FIAP.
Baseline preservada na tag `fase-2-final`.

O servico consome `OrderPlacedEvent`, simula o pagamento e publica
`PaymentProcessedEvent` no RabbitMQ para CatalogAPI e no SQS para a Lambda:

- `Price <= Payment__ApprovalLimit`: `Approved`;
- `Price > Payment__ApprovalLimit`: `Rejected`.

Mensagens malformadas nao representam pagamentos rejeitados. IDs vazios, data
ausente, e-mail invalido ou preco menor ou igual a zero falham o consumo e sao
encaminhados pelo MassTransit para a fila `payments-order-placed_error`.

Nao existe integracao com um provedor de pagamentos nem persistencia de dados.

A publicacao e sequencial: RabbitMQ primeiro, SQS depois. Qualquer falha propaga
para o consumer. MassTransit repete apos 5, 15 e 30 segundos; mensagens invalidas
nao sao repetidas. Apos esgotar retries, a mensagem vai para
`payments-order-placed_error` e requer diagnostico e reprocessamento operacional.
Nao foi criado um novo outbox. Uma falha SQS apos sucesso RabbitMQ pode repetir
o resultado no CatalogAPI; sua protecao de biblioteca e preservada. OrderId e
estavel em todas as tentativas; o timestamp pode mudar, sem mudar a deduplicacao
da Lambda. Nao ha transacao atomica entre os dois destinos.

## Tecnologias

- .NET 8
- ASP.NET Core Minimal APIs
- MassTransit 8.5.10
- RabbitMQ
- AWS SDK SQS 4.0.100.13
- Serilog
- xUnit
- Docker
- Kubernetes

## Estrutura

```text
.
├── src/FCG.Payments.Api/
├── tests/FCG.Payments.Tests/
├── k8s/
├── Dockerfile
└── TechChallenge.Payments.sln
```

## Contratos de integracao

Os contratos usam o namespace `FCG.IntegrationEvents.V1`.

`OrderPlacedEvent`:

- `OrderId`
- `OccurredAtUtc`
- `UserId`
- `UserEmail`
- `GameId`
- `Price`

`PaymentProcessedEvent`:

- `OrderId`
- `ProcessedAtUtc`
- `UserId`
- `UserEmail`
- `GameId`
- `Price`
- `Status`

## Variaveis de ambiente

| Variavel | Sensivel | Descricao |
|---|---|---|
| `RabbitMq__Host` | Nao | Host do RabbitMQ. |
| `RabbitMq__Port` | Nao | Porta AMQP, normalmente `5672`. |
| `RabbitMq__VirtualHost` | Nao | Virtual host do broker. |
| `RabbitMq__Username` | Sim | Usuario do RabbitMQ. |
| `RabbitMq__Password` | Sim | Senha do RabbitMQ. |
| `RabbitMq__OrderPlacedQueue` | Nao | Fila que recebe `OrderPlacedEvent`. |
| `Payment__ApprovalLimit` | Nao | Limite inclusivo para aprovacao; padrao `100.00`. |
| `Sqs__Region` | Nao | Regiao AWS; us-east-1. |
| `Sqs__QueueUrl` | Nao | URL HTTPS da fila de pagamento na mesma regiao. |
| `AWS_ACCESS_KEY_ID` | Sim | Credencial temporaria da role payments publisher. |
| `AWS_SECRET_ACCESS_KEY` | Sim | Segredo temporario. |
| `AWS_SESSION_TOKEN` | Sim | Token STS; renovar antes de expirar. |

Credenciais nao devem ser adicionadas ao `appsettings.json`.

## Executar localmente

Defina as credenciais do broker:

```bash
export RabbitMq__Username='<rabbitmq-username>'
export RabbitMq__Password='<rabbitmq-password>'
export Sqs__Region=us-east-1
export Sqs__QueueUrl='https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/fcg-fase3-payment-processed'
# Injete as tres variaveis AWS temporarias da role de publicacao.
```

Execute:

```bash
dotnet restore
dotnet run --project src/FCG.Payments.Api/FCG.Payments.Api.csproj
```

Endpoints operacionais:

- `GET /`
- `GET /health`
- `GET /swagger/index.html` em Development

Nao existem endpoints HTTP de negocio. Todas as requisicoes HTTP disponiveis sao
anonimas; o processamento funcional ocorre por eventos.

## Testes

```bash
dotnet run --project tests/FCG.Payments.Tests/FCG.Payments.Tests.csproj
```

O projeto usa o runner nativo do xUnit v3. Os testes validam:

- aprovacao no limite;
- rejeicao acima do limite;
- preservacao dos dados do pedido;
- falha para mensagens invalidas;
- publicacao de um unico resultado para pedido valido;
- ausencia de publicacao para pedido invalido.
- contratos identicos no RabbitMQ e no JSON SQS;
- falha SQS apos sucesso RabbitMQ e retry preservando OrderId;
- falha RabbitMQ propagada e recuperacao posterior.

O ensaio real esta em `compose.stage4.yaml` e `scripts/stage4-smoke.ps1` no
[repositorio de orquestracao](https://github.com/arthuurqueirozz/tech-challenge-3-orchestration).
As credenciais de deploy ficam no host; o container recebe somente uma sessao
STS da role com SendMessage na fila de pagamento.

## Docker

Na raiz do repositorio:

```bash
docker build -t tech-challenge-2-payments-api:local .
```

O Dockerfile usa build multi-stage e executa a imagem final com ASP.NET Core
Runtime 8 e usuario nao root.

## Kubernetes

Os manifests ficam em `/k8s`:

- `deployment.yaml`;
- `service.yaml`;
- `configmap.yaml`;
- `secret.example.yaml`.

Crie o arquivo local de Secret:

```bash
cp k8s/secret.example.yaml k8s/secret.local.yaml
```

Substitua os placeholders e aplique:

```bash
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.local.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

O arquivo `secret.local.yaml` e ignorado pelo Git.
