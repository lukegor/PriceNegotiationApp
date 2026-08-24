namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SecretKey { get; init; }

    public int ExpiryMinutes { get; init; } = 60;
}

