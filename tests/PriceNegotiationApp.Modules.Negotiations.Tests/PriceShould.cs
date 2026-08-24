using PriceNegotiationApp.Modules.Negotiations.Domain;
using Shouldly;
using Vogen;
using Xunit;

namespace PriceNegotiationApp.Modules.Negotiations.Tests;

public class PriceShould
{
    [Fact]
    public void Accept_positive_values()
    {
        var price = Price.From(19.99m);
        price.Value.ShouldBe(19.99m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reject_zero_or_negative_values(decimal value) =>
        Should.Throw<ValueObjectValidationException>(() => Price.From(value));
}

