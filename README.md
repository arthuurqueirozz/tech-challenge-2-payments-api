# FIAP Cloud Games - PaymentsAPI

Microsservico de pagamentos da Fase 2 do Tech Challenge FIAP.

O servico consome `OrderPlacedEvent`, simula o pagamento e publica
`PaymentProcessedEvent`:

- `Price <= Payment__ApprovalLimit`: `Approved`;
- `Price > Payment__ApprovalLimit`: `Rejected`.

Mensagens malformadas nao representam pagamentos rejeitados. IDs vazios, data
ausente, e-mail invalido ou preco menor ou igual a zero falham o consumo e sao
encaminhados pelo MassTransit para a fila `payments-order-placed_error`.

Nao existe integracao com um provedor de pagamentos nem persistencia de dados.

## Tecnologias

- .NET 8
- ASP.NET Core Minimal APIs
- MassTransit 8.5.10
- RabbitMQ
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

Credenciais nao devem ser adicionadas ao `appsettings.json`.

## Executar localmente

Defina as credenciais do broker:

```bash
export RabbitMq__Username='<rabbitmq-username>'
export RabbitMq__Password='<rabbitmq-password>'
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
