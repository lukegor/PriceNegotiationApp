using PriceNegotiationApp.SharedKernel;
using Vogen;
using PriceVo = PriceNegotiationApp.Modules.Catalog.Domain.Price;

namespace PriceNegotiationApp.Modules.Catalog.Domain;


public sealed class Product
{
    public const int MaxNameLength = 200;

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
        EnsureValid(name, price);
        Id = id;
        Name = name.Trim();
        Price = PriceVo.From(price).Value;
    }

    public static Product Create(string name, decimal price) =>
        new(ProductId.From(Guid.CreateVersion7()), name, price);

    /// <summary>Applies changes. Returns false when nothing changed (PUT stays idempotent).</summary>
    public bool Update(string name, decimal price)
    {
        EnsureValid(name, price);
        var validated = PriceVo.From(price).Value;
        var trimmed = name.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal) && Price == validated)
        {
            return false;
        }

        Name = trimmed;
        Price = validated;
        return true;
    }

    private static void EnsureValid(string? name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name must not be empty.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new DomainException($"Product name must not exceed {MaxNameLength} characters.");
        }

        PriceVo.From(price);
    }
}



