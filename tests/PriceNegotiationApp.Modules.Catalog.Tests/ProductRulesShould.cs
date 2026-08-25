using PriceNegotiationApp.Modules.Catalog.Domain;
using PriceNegotiationApp.SharedKernel;
using PriceNegotiationApp.TestKit;
using Shouldly;
using Vogen;
using Xunit;

namespace PriceNegotiationApp.Modules.Catalog.Tests;

public class ProductRulesShould
{
    // Semantic partitions stay inline: null/empty/whitespace and zero/negative are
    // distinct validation branches; 'x' x201 is the length boundary.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_null_or_whitespace_name(string? name) =>
        Should.Throw<DomainException>(() => Product.Create(name!, Fuzz.NewFaker().Price()));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_price(decimal price) =>
        Should.Throw<ValueObjectValidationException>(
            () => Product.Create(Fuzz.NewFaker().ProductName(), price));

    [Fact]
    public void Create_rejects_name_over_200_characters() =>
        Should.Throw<DomainException>(() => Product.Create(new string('x', 201), Fuzz.NewFaker().Price()));

    [Fact]
    public void Create_trims_surrounding_whitespace_and_assigns_id_and_price()
    {
        var faker = Fuzz.NewFaker();
        var rawName = $"  {faker.ProductName()}  ";
        var price = faker.Price();

        var product = Product.Create(rawName, price);

        product.Name.ShouldBe(rawName.Trim());
        product.Id.Value.ShouldNotBe(Guid.Empty);
        product.Price.ShouldBe(price);
    }

    [Fact]
    public void Update_returns_true_and_applies_changes_when_changed()
    {
        var faker = Fuzz.NewFaker();
        var originalName = faker.ProductName();
        var originalPrice = faker.Price();
        var product = Product.Create(originalName, originalPrice);
        var newName = faker.ProductName();
        var newPrice = faker.Price();
        Fuzz.Dump("update-pair", new { originalName, originalPrice, newName, newPrice });

        // Collision-immune: Bogus could legitimately generate identical values.
        var expectedChanged =
            !string.Equals(originalName, newName, StringComparison.Ordinal) || originalPrice != newPrice;

        var changed = product.Update(newName, newPrice);

        changed.ShouldBe(expectedChanged);
        product.Name.ShouldBe(newName);
        product.Price.ShouldBe(newPrice);
    }

    [Fact]
    public void Update_returns_false_when_identical()
    {
        var faker = Fuzz.NewFaker();
        var name = faker.ProductName();
        var price = faker.Price();
        var product = Product.Create(name, price);

        var changed = product.Update(name, price);

        changed.ShouldBeFalse();
    }
}
