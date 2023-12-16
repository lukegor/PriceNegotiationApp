using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PriceNegotiationApp.Auth;
using PriceNegotiationApp.Extensions;
using PriceNegotiationApp.Initializers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
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

			builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
			builder.Services.AddScoped<JwtManager>();

			builder.Services.AddScoped<MainInitializer>();

			builder.Services.AddScoped<AuthService>();
			builder.Services.AddScoped<IProductService, ProductService>();
			builder.Services.AddScoped<INegotiationService, NegotiationService>();
			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.ConfigureSwagger();

			builder.Services.AddHttpContextAccessor();

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
