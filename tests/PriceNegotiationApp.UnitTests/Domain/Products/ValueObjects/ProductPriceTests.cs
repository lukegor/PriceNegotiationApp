using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Products.ValueObjects
{
    public class ProductPriceTests
    {
        [Fact]
        public void ProductPriceConstructor_WhenPriceIsValid_ShouldCreate()
        {
            var price = new ProductPrice(100);
            Assert.Equal(100, price.Value);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void ProductPriceConstructor_WhenPriceIsInvalid_ShouldThrowDomainException(decimal invalidPrice)
        {
            Assert.Throws<DomainException>(() => new ProductPrice(invalidPrice));
        }
    }
}
