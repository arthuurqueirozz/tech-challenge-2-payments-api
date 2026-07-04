using FCG.IntegrationEvents.V1;

namespace FCG.Payments.Api.Messaging;

public interface IPaymentProcessedPublisher
{
    Task PublishAsync(
        PaymentProcessedEvent message,
        CancellationToken cancellationToken);
}
