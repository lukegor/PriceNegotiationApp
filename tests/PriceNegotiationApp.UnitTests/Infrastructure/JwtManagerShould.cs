using Microsoft.Extensions.Options;
using PriceNegotiationApp.Infrastructure.Auth;
using Xunit;

namespace PriceNegotiationApp.UnitTests.Infrastructure;

public class JwtManagerShould
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task Generate_token_with_sub_email_role_and_expiry()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = new string('k', 48),
            ExpiryMinutes = 30,
        });
        var clock = new FixedTimeProvider();
        var sut = new JwtManager(options, clock);

        var (token, expiresAtUtc) = await sut.GenerateAsync(Guid.NewGuid(), "user@test.dev", ["Customer"]);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token.Split('.').Length);
        var expected = clock.GetUtcNow().AddMinutes(30);
        Assert.True((expiresAtUtc - expected).Duration() < TimeSpan.FromSeconds(1));
    }
}

