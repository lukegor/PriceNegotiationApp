using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace PriceNegotiationApp.Api;

/// <summary>
/// JSON body for /health/ready naming every dependency and its verdict.
/// Failure detail goes to logs only; the anonymous body stays free of it.
/// </summary>
public static class ReadyHealthReport
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(ReadyHealthReport));

        foreach (var (name, entry) in report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy))
        {
            logger.LogWarning("Readiness check '{Check}' is unhealthy: {Detail}",
                name, entry.Description ?? entry.Exception?.Message);
        }

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    durationMs = entry.Value.Duration.TotalMilliseconds,
                }),
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
}
