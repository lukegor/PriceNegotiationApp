using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Catalog;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Identity;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using Scalar.AspNetCore;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

namespace PriceNegotiationApp.AppHost.Extensions;

public static class WebApplicationBuilderExtensions
{
    public const string CorsPolicy = "api";

    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        // Migrations must run before any module seeder (hosted services start in registration order).
        builder.Services.AddHostedService<Composition.MigrationHostedService>();

        builder.Host.UseSerilog((context, _, logConfiguration) => logConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine("logs", "api-.log"), rollingInterval: RollingInterval.Day));

        builder.Services.AddIdentityModule(configuration);
        builder.Services.AddCatalogModule(configuration);
        builder.Services.AddNegotiationsModule(configuration);
        builder.Services.AddScoped<PriceNegotiationApp.Modules.Negotiations.Ports.IProductPriceProvider,
            Composition.CatalogToNegotiations>();

        builder.Services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context =>
                    context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier))
            .AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection("Jwt").Get<JwtSettings>()!;
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });
        builder.Services.AddAuthorization();

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        }

        var rateLimits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                         ?? new RateLimitingOptions();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(Policies.AuthRateLimitPolicy, windowOptions =>
            {
                windowOptions.PermitLimit = rateLimits.AuthPermitLimit;
                windowOptions.Window = TimeSpan.FromMinutes(1);
                windowOptions.QueueLimit = 0;
            });
        });

        builder.Services.AddOutputCache(options => options.AddPolicy(Policies.ShortCachePolicy,
            policy => policy.Expire(TimeSpan.FromSeconds(30))
                .SetVaryByQuery("search", "minPrice", "maxPrice", "sortBy", "sortDesc", "page", "pageSize")));

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddDbContextCheck<IdentityModuleDbContext>("database-identity", tags: ["ready"])
            .AddDbContextCheck<CatalogDbContext>("database-catalog", tags: ["ready"])
            .AddDbContextCheck<NegotiationsDbContext>("database-negotiations", tags: ["ready"]);

        builder.Services.AddOpenApi();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("PriceNegotiationApp.AppHost"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation())
            .UseOtlpExporter();

        return builder;
    }
}

