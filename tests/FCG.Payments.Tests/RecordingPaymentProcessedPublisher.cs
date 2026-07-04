using System.Collections.Concurrent;
using FCG.IntegrationEvents.V1;
using FCG.Payments.Api.Messaging;

namespace FCG.Payments.Tests;

internal sealed class RecordingPaymentProcessedPublisher
    : IPaymentProcessedPublisher
{
    private readonly ConcurrentQueue<PaymentProcessedEvent> _messages = new();

    public IReadOnlyCollection<PaymentProcessedEvent> Messages =>
        _messages.ToArray();

    public Task PublishAsync(
        PaymentProcessedEvent message,
        CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
