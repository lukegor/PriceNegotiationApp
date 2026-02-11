using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Infrastructure.Data;

namespace PriceNegotiationApp.IntegrationTests
{
    public class IntegrationTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<IAppDbContext, AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForIntegrationTesting" + Guid.CreateVersion7().ToString());
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            });
        }
    }
}
