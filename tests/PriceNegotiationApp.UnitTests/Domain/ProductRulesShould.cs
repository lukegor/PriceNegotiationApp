using Xunit;
using Bogus;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Models;
using Vogen;

namespace PriceNegotiationApp.UnitTests.Domain;

public class ProductRulesShould
{
    private readonly Faker _faker = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_null_or_whitespace_name(string? name) =>
        Assert.Throws<DomainException>(() => Product.Create(name!, 10m));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_price(decimal price) =>
        Assert.Throws<ValueObjectValidationException>(() => Product.Create("Thing", price));

    [Fact]
    public void Create_trims_name_and_assigns_id()
    {
        var product = Product.Create("  Keyboard  ", 99.5m);

        Assert.Equal("Keyboard", product.Name);
        Assert.NotEqual(Guid.Empty, product.Id.Value);
        Assert.Equal(99.5m, product.Price);
    }

    [Fact]
    public void Update_returns_true_and_applies_changes_when_changed()
    {
        var product = Product.Create("Old", 10m);

        var changed = product.Update("New", 20m);

        Assert.True(changed);
        Assert.Equal("New", product.Name);
        Assert.Equal(20m, product.Price);
    }

    [Fact]
    public void Update_returns_false_when_identical()
    {
        var product = Product.Create("Same", 10m);

        var changed = product.Update("Same", 10m);

        Assert.False(changed);
    }
}

