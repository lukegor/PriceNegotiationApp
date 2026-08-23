namespace PriceNegotiationApp.Api.Contracts;

public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
