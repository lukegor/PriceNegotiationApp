using Microsoft.OpenApi.Models;
using System.Reflection;

namespace PriceNegotiationApp.Extensions
{
	public static class SwaggerServiceExtensions
	{
		public static void ConfigureSwagger(this IServiceCollection services)
		{
			services.AddSwaggerGen(options =>
			{
				options.SwaggerDoc("v1", new OpenApiInfo
				{
					Title = "Price Negotiation App",
					Version = "v1",
					Contact = new OpenApiContact
					{
						Name = "Łukasz Górski",
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
		}
	}
}
