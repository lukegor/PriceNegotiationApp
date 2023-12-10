using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using System.Reflection;

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
			builder.Services.AddScoped<ProductService>();
			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen(options =>
			{
				//options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
				//{

				//});

				// generate docs from xml comments to drive Swagger docs
				var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
				var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

				options.IncludeXmlComments(xmlPath);
			});

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
