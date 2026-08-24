namespace PriceNegotiationApp.BuildingBlocks;

/// <summary>Shared policy names so host registrations and module endpoint annotations agree.</summary>
public static class Policies
{
    public const string AuthRateLimitPolicy = "auth";

    public const string ShortCachePolicy = "short";
}
