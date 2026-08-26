namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    /// <summary>EC P-256 private key, PKCS#8 PEM; newlines may be literal or \n-escaped.</summary>
    public required string PrivateKey { get; init; }

    public int ExpiryMinutes { get; init; } = 60;
}

