using PriceNegotiationApp.Api.Extensions;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

// Plain unit facts for Api-owned validators; no Docker container required.
public class ConfigurationValidationShould
{
    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(int.MaxValue)]
    public void Accept_permit_limits_of_at_least_one(int limit) =>
        new RateLimitingOptionsValidator()
            .Validate(null, new RateLimitingOptions { AuthPermitLimit = limit })
            .Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Reject_non_positive_permit_limits(int limit) =>
        new RateLimitingOptionsValidator()
            .Validate(null, new RateLimitingOptions { AuthPermitLimit = limit })
            .Failed.ShouldBeTrue();

    [Fact]
    public void Accept_well_formed_cors_origins() =>
        Should.NotThrow(() => CorsOriginsGuard.EnsureValid(
            ["https://app.example.com", "http://localhost:3000"]));

    [Fact]
    public void Tolerate_null_or_empty_cors_lists() =>
        Should.NotThrow(() => CorsOriginsGuard.EnsureValid(null));

    [Theory]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://")]
    public void Reject_malformed_cors_origins(string origin) =>
        Should.Throw<InvalidOperationException>(
                () => CorsOriginsGuard.EnsureValid([origin]))
            .Message.ShouldContain(origin);
}
