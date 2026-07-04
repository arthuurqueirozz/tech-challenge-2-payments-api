using FCG.IntegrationEvents.V1;
using MassTransit;

namespace FCG.Payments.Api.Messaging;

public sealed class MassTransitPaymentProcessedPublisher(
    IPublishEndpoint publishEndpoint)
    : IPaymentProcessedPublisher
{
    public Task PublishAsync(
        PaymentProcessedEvent message,
        CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, cancellationToken);
}
