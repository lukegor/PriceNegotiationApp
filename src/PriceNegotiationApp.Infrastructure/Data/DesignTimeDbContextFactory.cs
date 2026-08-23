using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PriceNegotiationApp.Infrastructure.Persistence;

namespace PriceNegotiationApp.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString")
                               ?? "Host=localhost;Port=5432;Database=pricenego_design;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
