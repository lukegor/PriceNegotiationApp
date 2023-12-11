using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PriceNegotiationApp.Auth;
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
			builder.Services.AddScoped<ProductService>();
			builder.Services.AddScoped<NegotiationService>();
			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen(options =>
			{
				options.SwaggerDoc("v1", new OpenApiInfo
				{
					Title = "Price Negotiation App",
					Version = "v1",
					Contact = new OpenApiContact
					{
						Name = "£ukasz Górski",
						Email = "lukaszgorski02@gmail.com",
						Url = new Uri("https://www.linkedin.com/in/lukasz-gorski-lukegor/")
					},
					License = new OpenApiLicense
					{
						Name = "Apache License 2.0",
						Url = new Uri("https://opensource.org/license/apache-2-0/")
					}
				});

				options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
				{
					Name = "Authorization",
					Description = "Authorization header using the Bearer scheme for JWT",
					In = ParameterLocation.Header
				});

				options.AddSecurityRequirement(new OpenApiSecurityRequirement
				{
					{
						new OpenApiSecurityScheme
						{
							Reference = new OpenApiReference
							{
								Type = ReferenceType.SecurityScheme,
								Id = "Bearer"
							}
						},
						new string[] {}
					}
				});

				// generate docs from xml comments to drive Swagger docs
				var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
				var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

				options.IncludeXmlComments(xmlPath);
			});

			builder.Services.AddHttpContextAccessor();

			var serviceProvider = builder.Services.BuildServiceProvider(); ;
			using (var scope = serviceProvider.CreateScope())
			{
				var dbInitializer = scope.ServiceProvider.GetRequiredService<MainInitializer>();
				dbInitializer.InitializeRolesAsync().Wait(); // Synchronously wait for completion
				dbInitializer.InitializeAdminUserAsync().Wait();
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
