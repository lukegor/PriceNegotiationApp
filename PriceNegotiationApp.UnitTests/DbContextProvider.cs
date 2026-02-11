using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using PriceNegotiationApp.Infrastructure.Data;

namespace PriceNegotiationApp.UnitTests
{
    public static class DbContextProvider
    {
        public static AppDbContext GetInMemoryDbContext()
        {
            string dbName = $"TestDb_{Guid.NewGuid()}";

            var environment = CreateDefaultDevelopmentEnvironment();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName);

            if (environment.IsDevelopment())
            {
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();
            }

            return new AppDbContext(optionsBuilder.Options);
        }

        private static IWebHostEnvironment CreateDefaultDevelopmentEnvironment()
        {
            var substituteEnvironment = Substitute.For<IWebHostEnvironment>();
            substituteEnvironment.EnvironmentName.Returns("Development");
            return substituteEnvironment;
        }
    }
}
