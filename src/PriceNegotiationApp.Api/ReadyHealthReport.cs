using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PriceNegotiationApp.Api;

/// <summary>JSON body for /health/ready naming every dependency and its verdict.</summary>
public static class ReadyHealthReport
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status == HealthStatus.Healthy
                    ? (object)new
                    {
                        status = entry.Value.Status.ToString(),
                        durationMs = entry.Value.Duration.TotalMilliseconds,
                    }
                    : new
                    {
                        status = entry.Value.Status.ToString(),
                        durationMs = entry.Value.Duration.TotalMilliseconds,
                        description = entry.Value.Description ?? entry.Value.Exception?.Message,
                    }),
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
}
