using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog;
using PriceNegotiationApp.Modules.Identity;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
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
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/.well-known", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/favicon", StringComparison.OrdinalIgnoreCase);

    internal sealed record JwksResponse(IReadOnlyList<JwkKey> Keys);

    // Deliberate DTO: serializes exactly the five public fields, so private material
    // can never leak even if JsonWebKey grows properties later.
    internal sealed record JwkKey(string Kty, string Crv, string X, string Y, string Kid);

    private static void MapModules(this WebApplication app)
    {
        app.MapGet("/.well-known/jwks.json", (EcSigningKey signingKey) => TypedResults.Json(
                new JwksResponse([new JwkKey(
                    signingKey.PublicJwk.Kty,
                    signingKey.PublicJwk.Crv,
                    signingKey.PublicJwk.X,
                    signingKey.PublicJwk.Y,
                    signingKey.Kid)])))
            .AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready"),
            ResponseWriter = ReadyHealthReport.WriteAsync,
        });
        app.MapAuthEndpoints();
        app.MapCatalogEndpoints();
        app.MapNegotiationsEndpoints();
    }
}








