using Bogus;
using PriceNegotiationApp.Modules.Catalog.Domain;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Catalog.Tests;

public class UpdateIdempotencyShould
{
    private static readonly Faker Faker = new();

    [Fact]
    public void Return_false_when_nothing_changed()
    {
        var name = Faker.Commerce.ProductName();
        var price = Faker.Random.Decimal(1m, 1_000m);
        var product = Product.Create(name, price);

        var changed = product.Update(name, price);

        changed.ShouldBeFalse();
    }

    [Fact]
    public void Return_true_when_only_whitespace_differs()
    {
        var padded = $"{Faker.Commerce.ProductName()}   ";
        var product = Product.Create(Faker.Commerce.ProductName(), 10m);

        var changed = product.Update(padded, 10m);

        changed.ShouldBeTrue();
        product.Name.ShouldBe(padded.Trim());
    }
}
