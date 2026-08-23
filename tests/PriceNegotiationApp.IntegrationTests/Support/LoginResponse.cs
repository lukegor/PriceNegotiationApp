namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string Email, IReadOnlyList<string> Roles);
