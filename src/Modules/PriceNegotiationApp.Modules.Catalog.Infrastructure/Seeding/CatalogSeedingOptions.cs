namespace PriceNegotiationApp.Modules.Catalog.Infrastructure.Seeding;

internal sealed class CatalogSeedingOptions
{
    public const string SectionName = "Seeding";

    public bool SeedSampleProducts { get; init; }
}
