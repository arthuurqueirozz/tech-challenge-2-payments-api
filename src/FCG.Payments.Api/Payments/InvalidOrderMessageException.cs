namespace FCG.Payments.Api.Payments;

public sealed class InvalidOrderMessageException(string message)
    : Exception(message);
