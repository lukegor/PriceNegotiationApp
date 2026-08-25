namespace PriceNegotiationApp.Api.Extensions;

public static class CorsOriginsGuard
{
    /// <summary>Throws at startup when a configured CORS origin is not an absolute http(s) URI.</summary>
    public static void EnsureValid(IEnumerable<string?>? origins)
    {
        foreach (var origin in origins ?? [])
        {
            var valid = Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
                        && parsed.Scheme is "http" or "https";
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins entry '{origin}' is not a valid absolute http(s) URI.");
            }
        }
    }
}
