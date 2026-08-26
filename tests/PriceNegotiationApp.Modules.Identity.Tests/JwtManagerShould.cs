using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.TestKit;
using Shouldly;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class JwtManagerShould
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private static (JwtManager Manager, EcSigningKey Key) BuildSut(TimeProvider? clock = null)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            PrivateKey = ecdsa.ExportPkcs8PrivateKeyPem(),
            ExpiryMinutes = 30,
        });
        var key = new EcSigningKey(options);
        return (new JwtManager(options, key, clock ?? TimeProvider.System), key);
    }

    private static TokenValidationParameters Parameters(EcSigningKey key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "test-issuer",
        ValidateAudience = true,
        ValidAudience = "test-audience",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key.PublicJwk,
        ValidAlgorithms = [EcSigningKey.Algorithm],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };

    [Fact]
    public void Generate_es256_token_with_kid_email_role_and_expiry()
    {
        var email = Fuzz.Email();
        var (sut, _) = BuildSut(new FixedTimeProvider());

        var (token, expiresAtUtc) = sut.Generate(Guid.NewGuid(), email, ["Customer"]);

        var parts = token.Split('.');
        parts.Length.ShouldBe(3);
        var header = DecodeJson(parts[0]);
        header.GetProperty("alg").GetString().ShouldBe("ES256");
        header.GetProperty("kid").GetString().ShouldNotBeNullOrEmpty();
        DecodeJson(parts[1]).GetRawText().ShouldContain(email);
        var expected = new FixedTimeProvider().GetUtcNow().AddMinutes(30);
        (expiresAtUtc - expected).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Token_validates_against_the_published_public_key()
    {
        var userId = Guid.NewGuid();
        var (sut, key) = BuildSut();
        var (token, _) = sut.Generate(userId, Fuzz.Email(), ["Staff"]);

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, Parameters(key), out _);

        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.ShouldBe(userId.ToString());
        principal.FindFirst(ClaimTypes.Role)!.Value.ShouldBe("Staff");
    }

    [Fact]
    public void Token_signed_by_a_different_key_is_rejected()
    {
        var (sut, _) = BuildSut();
        var (_, stranger) = BuildSut();
        var (token, _) = sut.Generate(Guid.NewGuid(), Fuzz.Email(), []);

        Should.Throw<SecurityTokenException>(
            () => new JwtSecurityTokenHandler().ValidateToken(token, Parameters(stranger), out _));
    }

    private static JsonElement DecodeJson(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded));
    }
}
