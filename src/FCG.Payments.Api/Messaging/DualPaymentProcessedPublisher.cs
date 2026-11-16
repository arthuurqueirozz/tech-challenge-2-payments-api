using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FCG.IntegrationEvents.V1;
using FCG.Payments.Api.Configuration;
using MassTransit;
using Microsoft.Extensions.Options;

namespace FCG.Payments.Api.Messaging;

public sealed class DualPaymentProcessedPublisher(
    IPublishEndpoint rabbit,
    IAmazonSQS sqs,
    IOptions<SqsOptions> options) : IPaymentProcessedPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(PaymentProcessedEvent message, CancellationToken cancellationToken)
    {
        // Catalog must receive its result even when notifications fail. A partial success
        // propagates the exception so MassTransit retries with the same stable OrderId.
        await rabbit.Publish(message, cancellationToken);
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = options.Value.QueueUrl,
            MessageBody = JsonSerializer.Serialize(message, JsonOptions)
        }, cancellationToken);
    }
}
