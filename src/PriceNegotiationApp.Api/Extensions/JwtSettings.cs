namespace PriceNegotiationApp.Api.Extensions;

public sealed class JwtSettings
{
    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SecretKey { get; init; }
}

