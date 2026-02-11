using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Products.ValueObjects
{
    public class ProductPriceTests
    {
        [Fact]
        public void Constructor_ShouldCreate_WhenPriceIsValid()
        {
            var price = new ProductPrice(100);
            Assert.Equal(100, price.Value);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void ProductPriceConstructor_ShouldThrowDomainException_WhenPriceIsInvalid(decimal invalidPrice)
        {
            Assert.Throws<DomainException>(() => new ProductPrice(invalidPrice));
        }
    }
}
