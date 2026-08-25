using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog;
using PriceNegotiationApp.Modules.Identity;
using PriceNegotiationApp.Modules.Negotiations;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UsePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            // Enrichment runs at response completion, so HttpContext.User is populated.
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("UserId",
                    httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
                diagnosticContext.Set("Roles", string.Join(',',
                    httpContext.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)));
                diagnosticContext.Set("Endpoint", httpContext.GetEndpoint()?.DisplayName);
                diagnosticContext.Set("RemoteIp",
                    httpContext.Connection.RemoteIpAddress?.ToString());
            };
            options.GetLevel = (httpContext, elapsed, ex) => ex is not null
                ? LogEventLevel.Error
                : IsInfrastructurePath(httpContext.Request.Path)
                    ? LogEventLevel.Verbose
                    : elapsed > 500 ? LogEventLevel.Warning : LogEventLevel.Information;
        });
        app.UseStatusCodePages();

        app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseOutputCache();

        app.MapModules();

        return app;
    }

    private static bool IsInfrastructurePath(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/scalar") ||
        path.StartsWithSegments("/openapi") ||
        path.StartsWithSegments("/favicon");

    private static void MapModules(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
        app.MapAuthEndpoints();
        app.MapCatalogEndpoints();
        app.MapNegotiationsEndpoints();
    }
}








