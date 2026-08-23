using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PriceNegotiationApp.Application.Abstractions;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PriceNegotiationApp.Infrastructure.Auth;

public sealed class JwtManager(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenGenerator
{
    public Task<(string Token, DateTimeOffset ExpiresAtUtc)> GenerateAsync(Guid userId, string email, IReadOnlyCollection<string> roles)
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

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return Task.FromResult((new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc));
    }
}

