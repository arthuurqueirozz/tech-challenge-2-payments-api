using FCG.IntegrationEvents.V1;
using FCG.Payments.Api.Configuration;
using FCG.Payments.Api.Payments;
using Microsoft.Extensions.Options;

namespace FCG.Payments.Tests.Payments;

public sealed class OrderPaymentProcessorTests
{
    private static readonly DateTimeOffset ProcessedAt =
        new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Process_WhenPriceEqualsLimit_ApprovesAndPreservesOrderData()
    {
        var processor = CreateProcessor();
        var order = CreateOrder(100.00m);

        var result = processor.Process(order);

        Assert.Equal(PaymentStatuses.Approved, result.Status);
        Assert.Equal(order.OrderId, result.OrderId);
        Assert.Equal(order.UserId, result.UserId);
        Assert.Equal(order.UserEmail, result.UserEmail);
        Assert.Equal(order.GameId, result.GameId);
        Assert.Equal(order.Price, result.Price);
        Assert.Equal(ProcessedAt.UtcDateTime, result.ProcessedAtUtc);
    }

    [Fact]
    public void Process_WhenPriceExceedsLimit_Rejects()
    {
        var result = CreateProcessor().Process(CreateOrder(100.01m));

        Assert.Equal(PaymentStatuses.Rejected, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Process_WhenPriceIsNotPositive_Throws(decimal price)
    {
        var action = () => CreateProcessor().Process(CreateOrder(price));

        Assert.Throws<InvalidOrderMessageException>(action);
    }

    [Fact]
    public void Process_WhenOrderIdIsEmpty_Throws()
    {
        var order = CreateOrder(10.00m) with { OrderId = Guid.Empty };

        var action = () => CreateProcessor().Process(order);

        Assert.Throws<InvalidOrderMessageException>(action);
    }

    [Fact]
    public void Process_WhenEmailIsInvalid_Throws()
    {
        var order = CreateOrder(10.00m) with { UserEmail = "invalid-email" };

        var action = () => CreateProcessor().Process(order);

        Assert.Throws<InvalidOrderMessageException>(action);
    }

    internal static OrderPlacedEvent CreateOrder(decimal price) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Guid.NewGuid(),
            "ada@example.com",
            Guid.NewGuid(),
            price);

    internal static OrderPaymentProcessor CreateProcessor() =>
        new(
            Options.Create(new PaymentOptions { ApprovalLimit = 100.00m }),
            new FixedTimeProvider(ProcessedAt));
}
