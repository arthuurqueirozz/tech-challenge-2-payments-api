namespace FCG.IntegrationEvents.V1;

public sealed record OrderPlacedEvent(
    Guid OrderId,
    DateTime OccurredAtUtc,
    Guid UserId,
    string UserEmail,
    Guid GameId,
    decimal Price);
