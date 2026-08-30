namespace PriceNegotiationApp.Modules.Identity.Features.Auth.Register;

internal sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

internal sealed record RegistrationResponse(Guid UserId);
