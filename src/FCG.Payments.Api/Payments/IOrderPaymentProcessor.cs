using FCG.IntegrationEvents.V1;

namespace FCG.Payments.Api.Payments;

public interface IOrderPaymentProcessor
{
    PaymentProcessedEvent Process(OrderPlacedEvent order);
}
