using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.UriParser;
using Microsoft.OpenApi.Models;
using PriceNegotiationApp.Auth.Authentication.JWT;
using PriceNegotiationApp.Auth.Authorization.Resource_based;
using PriceNegotiationApp.Data;
using PriceNegotiationApp.Data.Initializers;
using PriceNegotiationApp.Extensions;
using PriceNegotiationApp.Middlewares;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Services.Providers;
using Serilog;
using System;
using System.Configuration;
using System.Reflection;
using System.Text;

namespace PriceNegotiationApp
{
    public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// configure Serilog
            var logger = new LoggerConfiguration()
				.ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(logger);

            builder.Services.AddControllers().AddOData(opt =>
			{
				opt.Select().Filter().OrderBy().Expand().SetMaxTop(100).Count();
				opt.AddRouteComponents("odata", ODataExtensions.GetEdmModel());
            });
			builder.Services.AddResponseCaching();

            // add database connection
            // for simplicity, using InMemory db
            builder.Services.AddDbContext<AppDbContext>(opt =>
			{
				opt.UseInMemoryDatabase(databaseName: "DbContext");

                if (builder.Environment.IsDevelopment())
                {
                    opt.EnableSensitiveDataLogging();
                    opt.EnableDetailedErrors();
                }
            });

			// add Microsoft.Identity
			builder.Services.AddIdentity<IdentityUser, IdentityRole>()
				.AddEntityFrameworkStores<AppDbContext>()
				.AddDefaultTokenProviders();

			var jwtSettings = builder.Configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>();

			builder.Services.AddAuthentication(opt =>
			{
				opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			}).AddJwtBearer(options =>
			{
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

            /// internally calls AddAuthorization(), connects to handler <see cref="IAuthorizationHandler"/>
            builder.Services.AddAuthorizationWithPolicies();
            builder.Services.AddSingleton<IAuthorizationHandler, NegotiationOperationsAuthorizationHandler>();

			// add handling JWT
			builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));
			builder.Services.AddScoped<JwtManager>();

			// add data initializer
			builder.Services.AddScoped<MainInitializer>();

			// add services
			builder.Services.AddScoped<IAuthService, AuthService>();
			builder.Services.AddScoped<IProductService, ProductService>();
			builder.Services.AddScoped<INegotiationService, NegotiationService>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IExecutionContext, HttpExecutionContext>();

			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddEndpointsApiExplorer();
            builder.Services.ConfigureSwagger();

            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

            var app = builder.Build();

            AddInitialData(app.Services);

            // Middlewares - HTTP request pipeline

            /// <see cref="app.Environment"/> checks ASPNETCORE_ENVIRONMENT variable
			/// (locally configured in launchSettings.json)
            if (app.Environment.IsDevelopment())
			{
				// OpenAPI/Swagger
				app.UseSwagger();
				app.UseSwaggerUI(opt =>
				{
					opt.EnablePersistAuthorization();
                });

                // display detailed error information in the browser when an unhandled exception occurs
                //app.UseDeveloperExceptionPage();
            }

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
