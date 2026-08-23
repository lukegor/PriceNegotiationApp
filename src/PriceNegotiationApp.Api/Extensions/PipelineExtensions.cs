using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PriceNegotiationApp.Api.Modules;
using Scalar.AspNetCore;
using Serilog;

namespace PriceNegotiationApp.Api.Extensions;

public static class PipelineExtensions
{
    public static WebApplication UsePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
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

    private static void MapModules(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
        app.MapAuthApi();
        app.MapProductsApi();
    }
}
