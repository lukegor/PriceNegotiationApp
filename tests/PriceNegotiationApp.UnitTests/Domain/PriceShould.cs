using PriceNegotiationApp.Domain.ValueObjects;
using Vogen;
using Xunit;

namespace PriceNegotiationApp.UnitTests.Domain;

public class PriceShould
{
    [Fact]
    public void Accept_positive_values()
    {
        var price = Price.From(19.99m);
        Assert.Equal(19.99m, price.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reject_zero_or_negative_values(decimal value) =>
        Assert.Throws<ValueObjectValidationException>(() => Price.From(value));
}

