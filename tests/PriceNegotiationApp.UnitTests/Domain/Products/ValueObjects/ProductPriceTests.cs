using Bogus;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Products.ValueObjects
{
    public class ProductPriceTests
    {
        private readonly Faker _faker = new("pl");

        [Fact]
        public void ProductPriceConstructor_WhenPriceIsValid_ShouldCreate()
        {
            var amount = _faker.Finance.Amount(1, 10000);
            var price = new ProductPrice(amount);
            Assert.Equal(amount, price.Value);
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
