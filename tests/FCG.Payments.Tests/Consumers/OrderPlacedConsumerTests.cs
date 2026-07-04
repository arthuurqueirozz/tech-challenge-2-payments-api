using FCG.IntegrationEvents.V1;
using FCG.Payments.Api.Consumers;
using FCG.Payments.Api.Payments;
using FCG.Payments.Tests.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace FCG.Payments.Tests.Consumers;

public sealed class OrderPlacedConsumerTests
{
    [Fact]
    public async Task ProcessAsync_WhenOrderIsValid_PublishesOneResult()
    {
        var publisher = new RecordingPaymentProcessedPublisher();
        var consumer = CreateConsumer(publisher);
        var order = OrderPaymentProcessorTests.CreateOrder(59.90m);

        await consumer.ProcessAsync(order, CancellationToken.None);

        var result = Assert.Single(publisher.Messages);
        Assert.Equal(order.OrderId, result.OrderId);
        Assert.Equal(PaymentStatuses.Approved, result.Status);
    }

    [Fact]
    public async Task ProcessAsync_WhenOrderIsInvalid_FailsWithoutPublishing()
    {
        var publisher = new RecordingPaymentProcessedPublisher();
        var consumer = CreateConsumer(publisher);
        var order = OrderPaymentProcessorTests.CreateOrder(0);

        await Assert.ThrowsAsync<InvalidOrderMessageException>(
            () => consumer.ProcessAsync(order, CancellationToken.None));

        Assert.Empty(publisher.Messages);
    }

    private static OrderPlacedConsumer CreateConsumer(
        RecordingPaymentProcessedPublisher publisher) =>
        new(
            OrderPaymentProcessorTests.CreateProcessor(),
            publisher,
            NullLogger<OrderPlacedConsumer>.Instance);
}
