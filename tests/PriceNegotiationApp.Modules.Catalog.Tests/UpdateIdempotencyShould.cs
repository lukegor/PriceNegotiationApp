using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Xunit;

namespace PriceNegotiationApp.Modules.Catalog.Tests;

public class UpdateIdempotencyShould
{
    [Fact]
    public void Return_false_when_nothing_changed()
    {
        var faker = Fuzz.NewFaker();
        var name = faker.ProductName();
        var price = faker.Price();
        var product = Product.Create(name, price);

        var changed = product.Update(name, price);

        changed.ShouldBeFalse();
    }

    [Fact]
    public void Return_true_when_only_whitespace_differs()
    {
        var faker = Fuzz.NewFaker();
        var padded = $"{faker.ProductName()}   ";
        var product = Product.Create(faker.ProductName(), faker.Price());

        var changed = product.Update(padded, product.Price);

        changed.ShouldBeTrue();
        product.Name.ShouldBe(padded.Trim());
    }
}
