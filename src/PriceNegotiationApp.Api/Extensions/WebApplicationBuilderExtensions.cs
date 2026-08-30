using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PriceNegotiationApp.Modules.Catalog;
using PriceNegotiationApp.Modules.Catalog.Persistence;
using PriceNegotiationApp.Modules.Identity;
using PriceNegotiationApp.Modules.Identity.Features.Auth;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;
using Scalar.AspNetCore;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

namespace PriceNegotiationApp.Api.Extensions;

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
        builder.Services.AddScoped<PriceNegotiationApp.Modules.Catalog.Ports.IProductPriceProvider,
            PriceNegotiationApp.Modules.Catalog.Adapters.ProductPriceProvider>();

        builder.Services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context =>
                    context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier))
            .AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, EcSigningKey>((bearer, jwt, signingKey) =>
            {
                bearer.MapInboundClaims = true;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey.PublicJwk,
                    ValidAlgorithms = [EcSigningKey.Algorithm],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });
        builder.Services.AddAuthorization();

        // Accepts all configuration shapes: JSON array, indexed keys
        // ("Cors:AllowedOrigins:0"), or one flat comma-separated value.
        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins");
        var origins = corsOrigins.GetChildren().Select(child => child.Value).OfType<string>().ToList();
        if (origins.Count == 0 && corsOrigins.Value is { } flatValue)
        {
            origins.AddRange(flatValue.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        CorsOriginsGuard.EnsureValid(origins);
        builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
            policy.WithOrigins([.. origins]).AllowAnyHeader().AllowAnyMethod()));

        var rateLimits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                         ?? new RateLimitingOptions();
        builder.Services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<RateLimitingOptions>,
            RateLimitingOptionsValidator>();
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
                .SetVaryByQuery("search", "minPrice", "maxPrice", "sortBy", "sortDesc", "page", "pageSize")
                .SetVaryByHeader("Origin")));

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddDbContextCheck<IdentityModuleDbContext>("database-identity", tags: ["ready"])
            .AddDbContextCheck<CatalogDbContext>("database-catalog", tags: ["ready"])
            .AddDbContextCheck<NegotiationsDbContext>("database-negotiations", tags: ["ready"]);

        builder.Services.AddOpenApi();

        // Telemetry ships only when a consumer is configured (Aspire dashboard overlay,
        // Grafana stack, or any OTLP endpoint). Prevents endless export retries against
        // localhost:4317 where nothing listens.
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("PriceNegotiationApp.Api"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation())
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation())
                .UseOtlpExporter();
        }

        return builder;
    }
}

