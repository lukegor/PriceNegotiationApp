using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PriceNegotiationApp.Auth.Authentication.JWT;
using PriceNegotiationApp.Auth.Authorization.Resource_based;
using PriceNegotiationApp.Extensions;
using PriceNegotiationApp.Initializers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Services.Providers;
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

			// Add services to the container.

			builder.Services.AddControllers();
			builder.Services.AddResponseCaching();

			builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("DbContext"));

			builder.Services.AddIdentity<IdentityUser, IdentityRole>()
				.AddEntityFrameworkStores<AppDbContext>()
				.AddDefaultTokenProviders();

			var jwtSettings = builder.Configuration.GetSection("JwtSettings");

			builder.Services.AddAuthentication(opt =>
			{
				opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			}).AddJwtBearer(options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = jwtSettings["validIssuer"],
					ValidAudience = jwtSettings["validAudience"],
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
						.GetBytes(jwtSettings.GetSection("securityKey").Value))
				};
			});

			builder.Services.AddAuthorization(opt =>
			{
                opt.AddPolicy(OperationRequirements.AdminOrStaffOrOwner.Name, policy =>
                    policy.Requirements.Add(OperationRequirements.AdminOrStaffOrOwner));

                opt.AddPolicy(OperationRequirements.CreateRequirement.Name, policy =>
					policy.Requirements.Add(OperationRequirements.CreateRequirement));
				opt.AddPolicy(OperationRequirements.ReadRequirement.Name, policy =>
					policy.Requirements.Add(OperationRequirements.ReadRequirement));
						//policy.RequireAssertion(context =>
						//	context.User.IsInRole("Admin") ||
						//	context.User.IsInRole("Staff") ||
						//	(context.User.IsInRole("Customer") && context.User.));

				opt.AddPolicy(OperationRequirements.UpdateRequirement.Name, policy => 
					policy.Requirements.Add(OperationRequirements.UpdateRequirement));
                opt.AddPolicy(OperationRequirements.DeleteRequirement.Name, policy =>
					policy.Requirements.Add(OperationRequirements.DeleteRequirement));
            });

			builder.Services.AddSingleton<IAuthorizationHandler, NegotiationOperationsAuthorizationHandler>();

			builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
			builder.Services.AddScoped<JwtManager>();

			builder.Services.AddScoped<MainInitializer>();

			builder.Services.AddScoped<AuthService>();
			builder.Services.AddScoped<IProductService, ProductService>();
			builder.Services.AddScoped<INegotiationService, NegotiationService>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IClaimsProvider, HttpContextClaimsProvider>();

			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.ConfigureSwagger();

			var serviceProvider = builder.Services.BuildServiceProvider(); ;
			using (var scope = serviceProvider.CreateScope())
			{
				var dbInitializer = scope.ServiceProvider.GetRequiredService<MainInitializer>();
				dbInitializer.InitializeRolesAsync().Wait(); // Synchronously wait for completion
				dbInitializer.InitializeAdminUserAsync().Wait();
				dbInitializer.InitializeStaffUserAsync().Wait();
			}

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
			{
				app.UseSwagger();
				app.UseSwaggerUI();
			}

			app.UseHttpsRedirection();

			app.UseResponseCaching();

			app.UseAuthorization();


			app.MapControllers();

			app.Run();
		}
	}
}
