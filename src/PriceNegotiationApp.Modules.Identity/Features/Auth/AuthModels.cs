namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed record RegistrationResponse(Guid UserId);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
