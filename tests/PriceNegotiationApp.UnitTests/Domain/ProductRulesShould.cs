using Xunit;
using Bogus;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain;

public class ProductRulesShould
{
    private readonly Faker _faker = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_null_or_whitespace_name(string? name) =>
        Assert.Throws<DomainException>(() => Product.Create(name!, Price.From(10m)));

    [Fact]
    public void Create_trims_name_and_assigns_id()
    {
        var product = Product.Create("  Keyboard  ", Price.From(99.5m));

        Assert.Equal("Keyboard", product.Name);
        Assert.NotEqual(Guid.Empty, product.Id.Value);
        Assert.Equal(99.5m, product.Price.Value);
    }

    [Fact]
    public void Update_returns_true_and_applies_changes_when_changed()
    {
        var product = Product.Create("Old", Price.From(10m));

        var changed = product.Update("New", Price.From(20m));

        Assert.True(changed);
        Assert.Equal("New", product.Name);
        Assert.Equal(20m, product.Price.Value);
    }

    [Fact]
    public void Update_returns_false_when_identical()
    {
        var product = Product.Create("Same", Price.From(10m));

        var changed = product.Update("Same", Price.From(10m));

        Assert.False(changed);
    }
}

