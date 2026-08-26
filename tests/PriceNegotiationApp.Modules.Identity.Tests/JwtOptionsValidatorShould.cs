using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Identity.Tests;

public class JwtOptionsValidatorShould
{
    private readonly JwtOptionsValidator _sut = new();

    [Fact]
    public void Accept_a_complete_configuration()
    {
        var result = _sut.Validate(null, new JwtOptions
        {
            Issuer = Fuzz.NewFaker().Internet.DomainName(),
            Audience = "price-negotiation-api",
            PrivateKey = "not-parsed-here",
            ExpiryMinutes = 30,
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Reject_blank_private_key()
    {
        var result = _sut.Validate(null, new JwtOptions
        {
            Issuer = "i",
            Audience = "a",
            PrivateKey = "   ",
            ExpiryMinutes = 30,
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f => f.Contains("PrivateKey"));
    }

    [Fact]
    public void Reject_non_positive_expiry()
    {
        var result = _sut.Validate(null, new JwtOptions
        {
            Issuer = "i",
            Audience = "a",
            PrivateKey = "pem",
            ExpiryMinutes = 0,
        });

        result.Failed.ShouldBeTrue();
    }
}
