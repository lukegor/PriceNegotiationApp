using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Product : Entity
{
    public ProductId Id { get; private set; }

    public string Name { get; private set; } = null!;

    public decimal Price { get; private set; }

    /// <summary>Optimistic-concurrency token mapped to PostgreSQL xmin.</summary>
    public uint Version { get; private set; }

    private Product()
    {
    }

    private Product(ProductId id, string name, decimal price)
    {
        CheckRule(new ProductNameMustNotBeEmpty(name));
        Id = id;
        Name = name.Trim();
        // Validates positivity through the value object even though persistence stores a plain numeric column.
        Price = ValueObjects.Price.From(price).Value;
    }

    public static Product Create(string name, decimal price) =>
        new(ProductId.From(Guid.CreateVersion7()), name, price);

    /// <summary>Applies changes. Returns false when nothing changed (PUT stays idempotent).</summary>
    public bool Update(string name, decimal price)
    {
        CheckRule(new ProductNameMustNotBeEmpty(name));
        var validated = ValueObjects.Price.From(price).Value;
        var trimmed = name.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal) && Price == validated)
        {
            return false;
        }

        Name = trimmed;
        Price = validated;
        return true;
    }
}

