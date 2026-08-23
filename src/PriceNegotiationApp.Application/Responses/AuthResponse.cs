namespace PriceNegotiationApp.Application.Responses;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles);
