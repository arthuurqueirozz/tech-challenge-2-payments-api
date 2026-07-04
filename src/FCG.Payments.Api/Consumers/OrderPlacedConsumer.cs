using FCG.IntegrationEvents.V1;
using FCG.Payments.Api.Messaging;
using FCG.Payments.Api.Payments;
using MassTransit;

namespace FCG.Payments.Api.Consumers;

public sealed class OrderPlacedConsumer(
    IOrderPaymentProcessor processor,
    IPaymentProcessedPublisher publisher,
    ILogger<OrderPlacedConsumer> logger)
    : IConsumer<OrderPlacedEvent>
{
    public Task Consume(ConsumeContext<OrderPlacedEvent> context) =>
        ProcessAsync(context.Message, context.CancellationToken);

    internal async Task ProcessAsync(
        OrderPlacedEvent message,
        CancellationToken cancellationToken)
    {
        var result = processor.Process(message);

        await publisher.PublishAsync(result, cancellationToken);

        logger.LogInformation(
            "Payment processed. OrderId: {OrderId}, UserId: {UserId}, GameId: {GameId}, Price: {Price}, Status: {Status}",
            result.OrderId,
            result.UserId,
            result.GameId,
            result.Price,
            result.Status);
    }
}
