namespace PriceNegotiationApp.Infrastructure.Seeding;

public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    public string AdminEmail { get; init; } = "admin@app.com";

    public string AdminPassword { get; init; } = string.Empty;

    public string StaffEmail { get; init; } = "staff@app.com";

    public string StaffPassword { get; init; } = string.Empty;

    public bool SeedSampleProducts { get; init; }
}
