using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PriceNegotiationApp.Api.Extensions;
using PriceNegotiationApp.Api.Providers;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Common.Identities;
using PriceNegotiationApp.Application.Negotiations;
using PriceNegotiationApp.Application.Products;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Application.Services;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Customers;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Infrastructure;
using PriceNegotiationApp.Infrastructure.Auth.Authentication.Jwt;
using PriceNegotiationApp.Infrastructure.Data;
using PriceNegotiationApp.Infrastructure.Data.Initializers;
using PriceNegotiationApp.Infrastructure.Identities;
using PriceNegotiationApp.Presentation;
using Scalar.AspNetCore;
using Serilog;
using System.Text;

namespace PriceNegotiationApp.Api
{
#pragma warning disable S1118 // Utility classes should not have public constructors
    public class Program
#pragma warning restore S1118 // Utility classes should not have public constructors
    {
        private static readonly string[] tags = new[] { "live" };

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // configure Serilog
            Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine($"SERILOG ERROR: {msg}"));
            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
                configuration.ReadFrom.Services(services);
            });

            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<AutoValidationFilter>();
            }).AddOData(opt =>
            {
                opt.Select().Filter().OrderBy().Expand().SetMaxTop(100).Count();
                opt.AddRouteComponents("odata", ODataExtensions.GetEdmModel());
            });

            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), tags: tags)
                .AddCheck("in-memory-db", () => HealthCheckResult.Healthy(), tags: new[] { "ready" });

            builder.Services.AddHealthChecksUI(setup =>
            {
                setup.AddHealthCheckEndpoint("API Health Checks", "/health/all");
            }).AddInMemoryStorage();

            builder.Services.AddResponseCaching();

            // add database connection
            // for simplicity, using InMemory db
            builder.Services.AddDbContext<IAppDbContext, AppDbContext>(opt =>
            {
                opt.UseInMemoryDatabase(databaseName: "DbContext");

                if (builder.Environment.IsDevelopment())
                {
                    opt.ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                    opt.EnableSensitiveDataLogging();
                    opt.EnableDetailedErrors();
                }
            });

            // add Microsoft.Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            // register JWT settings to be taken from AppSettings
            var jwtSection = builder.Configuration.GetSection(nameof(JwtSettings));
            builder.Services.AddOptions<JwtSettings>()
                .Bind(jwtSection)
                .ValidateOnStart();
            builder.Services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

            builder.Services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                var jwtSettings = jwtSection.Get<JwtSettings>();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.ValidIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                        .GetBytes(jwtSettings.SecurityKey))
                };
            });

            builder.Services.AddAuthorizationWithPolicies();

            builder.Services.AddSingleton(TimeProvider.System);

            builder.Services.AddScoped<JwtManager>();

            // add data initializer
            builder.Services.AddScoped<MainInitializer>();

            // add services
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<INegotiationService, NegotiationService>();

            builder.Services.AddScoped<INegotiationDomainService, NegotiationDomainService>();

            builder.Services.AddScoped<IIdentityService, IdentityService>();

            builder.Services.AddScoped<ProductFactory>();
            builder.Services.AddScoped<NegotiationFactory>();
            builder.Services.AddScoped<CustomerFactory>();

            builder.Services.AddScoped<IJwtTokenGenerator, JwtManager>();
            builder.Services.AddScoped<IIdGenerator, SystemIdGenerator>();

            builder.Services.AddSingleton<INegotiationPolicy, DefaultNegotiationPolicy>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IExecutionContext, HttpExecutionContext>();

            // Adds automatic registration of FluentValidation validators from the specified assembly
            builder.Services.AddValidatorsFromAssembly(typeof(RequestValidatorsAssemblyReference).Assembly);

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.ConfigureOpenApi();

            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

            var app = builder.Build();

            app.UseSerilogRequestLogging();

            AddInitialData(app.Services);

            // Middlewares - HTTP request pipeline

            // <see cref="app.Environment"/> checks ASPNETCORE_ENVIRONMENT variable
            // (locally configured in launchSettings.json)
            if (app.Environment.IsDevelopment())
            {
                // OpenAPI/Swagger
                app.MapOpenApi();
                app.MapScalarApiReference(opt =>
                {
                    opt.EnablePersistentAuthentication();
                });

#pragma warning disable S125 // Sections of code should not be commented out
                // display detailed error information in the browser when an unhandled exception occurs
                //app.UseDeveloperExceptionPage();
#pragma warning restore S125 // Sections of code should not be commented out
            }

            app.UseStatusCodePages();
            app.UseExceptionHandler();

            // Redirects HTTP requests to HTTPS
            app.UseHttpsRedirection();

            if (!app.Environment.IsDevelopment())
            {
                // appliable only to https
                app.UseHsts();
            }

            // Auth
            app.UseAuthentication();
            app.UseAuthorization(); // relies on UseAuthentication()

            // after Auth, may require Auth info
            app.UseResponseCaching();

            // last middleware, uses UseEndpoints internally
            app.MapControllers();

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = (check) => check.Tags.Contains("live"),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = (check) => check.Tags.Contains("ready"),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.MapHealthChecks("/health/all", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.MapHealthChecksUI(options => options.UIPath = "/dashboard");

            app.Run();
        }

        private static void AddInitialData(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<MainInitializer>();
                dbInitializer.InitializeRolesAsync().GetAwaiter().GetResult();
                dbInitializer.InitializeAdminUserAsync().GetAwaiter().GetResult();
                dbInitializer.InitializeStaffUserAsync().GetAwaiter().GetResult();
            }
        }
    }
}
