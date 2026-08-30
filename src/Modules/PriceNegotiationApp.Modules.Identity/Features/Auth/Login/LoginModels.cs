namespace PriceNegotiationApp.Modules.Identity.Features.Auth.Login;

internal sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

internal sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles);
