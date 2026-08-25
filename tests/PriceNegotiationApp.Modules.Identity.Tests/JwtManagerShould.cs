using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class JwtManagerShould
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void Generate_token_with_sub_email_role_and_expiry()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = new string('k', 48), // length is semantic; content irrelevant
            ExpiryMinutes = 30,
        });
        var clock = new FixedTimeProvider();
        var sut = new JwtManager(options, clock);
        var email = Fuzz.Email();

        var (token, expiresAtUtc) = sut.Generate(Guid.NewGuid(), email, ["Customer"]);

        var payloadJson = Base64UrlEncoder.Decode(token.Split('.')[1]);
        payloadJson.ShouldContain(email);
        token.Split('.').Length.ShouldBe(3);
        var expected = clock.GetUtcNow().AddMinutes(30);
        (expiresAtUtc - expected).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }
}

