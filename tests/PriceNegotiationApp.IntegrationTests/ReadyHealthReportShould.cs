using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PriceNegotiationApp.Api;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.IntegrationTests;

// Plain unit fact over the response writer; no Docker container required.
public class ReadyHealthReportShould
{
    [Fact]
    public async Task Never_leak_failure_detail_in_body_even_when_unhealthy()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();
        var secret = "password authentication failed for user 'postgres'";
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database-catalog"] = new(
                    HealthStatus.Unhealthy, secret, TimeSpan.FromMilliseconds(3),
                    new InvalidOperationException(secret), null),
                ["self"] = new(
                    HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(1), null, null),
            },
            totalDuration: TimeSpan.FromMilliseconds(4));

        await ReadyHealthReport.WriteAsync(context, report);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain(secret);
        body.ShouldNotContain("description");
        body.ShouldContain("\"Unhealthy\"");
    }
}
