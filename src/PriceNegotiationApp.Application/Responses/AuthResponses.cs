namespace PriceNegotiationApp.Application.Responses;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record RegistrationResponse(Guid UserId);

public sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
