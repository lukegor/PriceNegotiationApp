using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Negotiations.ValueObjects
{
    public class ProposedPriceTests
    {
        [Fact]
        public void ProposedPriceConstructor_WhenPriceIsValid_ShouldCreate()
        {
            var proposedPrice = new ProposedPrice(99.99m);
            Assert.Equal(99.99m, proposedPrice.Value);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void ProposedPriceConstructor_WhenPriceIsInvalid_ShouldThrowDomainException(decimal invalidPrice)
        {
            var exception = Assert.Throws<DomainException>(() => new ProposedPrice(invalidPrice));
            Assert.Equal("Proposed price cannot be negative or zero.", exception.Message);
        }
    }
}
