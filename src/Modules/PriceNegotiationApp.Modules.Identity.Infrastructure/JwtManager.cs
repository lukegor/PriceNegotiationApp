using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure;

internal sealed class JwtManager(IOptions<JwtOptions> options, EcSigningKey signingKey, TimeProvider clock)
{
    public (string Token, DateTimeOffset ExpiresAtUtc) Generate(Guid userId, string email, IReadOnlyCollection<string> roles)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Each call owns a fresh ECDsa, so provider caching must stay off: the
        // default cache would hand request #2 a provider wrapping request #1's
        // already-disposed key instance (both share the same Kid).
        using var ecdsa = signingKey.CreatePrivateEcdsa();
        var credentials = new SigningCredentials(
            new ECDsaSecurityKey(ecdsa) { KeyId = signingKey.Kid },
            EcSigningKey.Algorithm)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}



