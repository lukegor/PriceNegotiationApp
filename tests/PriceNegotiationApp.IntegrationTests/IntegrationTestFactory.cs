using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Infrastructure.Data;

namespace PriceNegotiationApp.IntegrationTests
{
    public class IntegrationTestFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<IAppDbContext, AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, options => { });
            });
        }

        public void ResetDatabase()
        {
            using var scope = Services.CreateScope();
            var context = (AppDbContext)scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        }
    }
}
