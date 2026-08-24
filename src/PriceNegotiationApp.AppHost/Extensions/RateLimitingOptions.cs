namespace PriceNegotiationApp.AppHost.Extensions;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; init; } = 30;
}

