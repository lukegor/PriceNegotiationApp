namespace PriceNegotiationApp.Modules.Identity.Application.Register;

internal sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

internal sealed record RegistrationResponse(Guid UserId);
