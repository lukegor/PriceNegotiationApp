namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

internal sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

internal sealed record RegistrationResponse(Guid UserId);

internal sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles);

internal sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
