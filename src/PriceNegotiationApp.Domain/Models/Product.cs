using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Product : Entity
{
    public ProductId Id { get; private set; }

    public string Name { get; private set; } = null!;

    public Price Price { get; private set; }

    /// <summary>Optimistic-concurrency token mapped to PostgreSQL xmin.</summary>
    public uint Version { get; private set; }

    private Product()
    {
    }

    private Product(ProductId id, string name, Price price)
    {
        CheckRule(new ProductNameMustNotBeEmpty(name));
        Id = id;
        Name = name.Trim();
        Price = price;
    }

    public static Product Create(string name, Price price) =>
        new(ProductId.From(Guid.CreateVersion7()), name, price);

    /// <summary>Applies changes. Returns false when nothing changed (PUT stays idempotent).</summary>
    public bool Update(string name, Price price)
    {
        CheckRule(new ProductNameMustNotBeEmpty(name));
        var trimmed = name.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal) && Price == price)
        {
            return false;
        }

        Name = trimmed;
        Price = price;
        return true;
    }
}
