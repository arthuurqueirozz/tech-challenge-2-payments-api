using System.Net.Mail;
using FCG.IntegrationEvents.V1;
using FCG.Payments.Api.Configuration;
using Microsoft.Extensions.Options;

namespace FCG.Payments.Api.Payments;

public sealed class OrderPaymentProcessor(
    IOptions<PaymentOptions> options,
    TimeProvider timeProvider)
    : IOrderPaymentProcessor
{
    public PaymentProcessedEvent Process(OrderPlacedEvent order)
    {
        Validate(order);

        var status = order.Price <= options.Value.ApprovalLimit
            ? PaymentStatuses.Approved
            : PaymentStatuses.Rejected;

        return new PaymentProcessedEvent(
            order.OrderId,
            timeProvider.GetUtcNow().UtcDateTime,
            order.UserId,
            order.UserEmail,
            order.GameId,
            order.Price,
            status);
    }

    private static void Validate(OrderPlacedEvent order)
    {
        if (order.OrderId == Guid.Empty)
        {
            throw new InvalidOrderMessageException("OrderId must not be empty.");
        }

        if (order.OccurredAtUtc == default)
        {
            throw new InvalidOrderMessageException("OccurredAtUtc must be provided.");
        }

        if (order.UserId == Guid.Empty)
        {
            throw new InvalidOrderMessageException("UserId must not be empty.");
        }

        if (order.GameId == Guid.Empty)
        {
            throw new InvalidOrderMessageException("GameId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(order.UserEmail)
            || !MailAddress.TryCreate(order.UserEmail, out _))
        {
            throw new InvalidOrderMessageException("UserEmail must be valid.");
        }

        if (order.Price <= 0)
        {
            throw new InvalidOrderMessageException("Price must be greater than zero.");
        }
    }
}
