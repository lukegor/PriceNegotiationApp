namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed record MeResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
