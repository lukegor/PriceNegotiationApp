using Bogus;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Models;
using Shouldly;
using Vogen;
using Xunit;

namespace PriceNegotiationApp.UnitTests.Domain;

public class ProductRulesShould
{
    private readonly Faker _faker = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_null_or_whitespace_name(string? name) =>
        Should.Throw<DomainException>(() => Product.Create(name!, 10m));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_price(decimal price) =>
        Should.Throw<ValueObjectValidationException>(() => Product.Create("Thing", price));

    [Fact]
    public void Create_rejects_name_over_200_characters() =>
        Should.Throw<DomainException>(() => Product.Create(new string('x', 201), 10m));

    [Fact]
    public void Create_trims_name_and_assigns_id()
    {
        var product = Product.Create("  Keyboard  ", 99.5m);

        product.Name.ShouldBe("Keyboard");
        product.Id.Value.ShouldNotBe(Guid.Empty);
        product.Price.ShouldBe(99.5m);
    }

    [Fact]
    public void Update_returns_true_and_applies_changes_when_changed()
    {
        var product = Product.Create("Old", 10m);

        var changed = product.Update("New", 20m);

        changed.ShouldBeTrue();
        product.Name.ShouldBe("New");
        product.Price.ShouldBe(20m);
    }

    [Fact]
    public void Update_returns_false_when_identical()
    {
        var product = Product.Create("Same", 10m);

        var changed = product.Update("Same", 10m);

        changed.ShouldBeFalse();
    }
}
