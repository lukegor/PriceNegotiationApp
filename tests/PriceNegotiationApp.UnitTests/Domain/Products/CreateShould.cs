using NSubstitute;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Products
{
    public class CreateShould
    {
        private readonly ProductFactory _productFactory;
        private readonly IIdGenerator _idGenerator;

        public CreateShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _productFactory = new ProductFactory(_idGenerator);
        }

        [Theory]
        [InlineData("Laptop", 100)]
        [InlineData("Mąka Poznańska", 5.99)]
        public void CreateProduct_WithValidData(string name, decimal priceValue)
        {
            // Arrange
            var expectedProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            _idGenerator.NewId().Returns(expectedProductId);

            var price = new ProductPrice(priceValue);

            // Act
            var product = _productFactory.Create(name, price);

            // Assert
            Assert.Equal(expectedProductId, product.Id.Value);
            Assert.Equal(name, product.Name);
            Assert.Equal(price, product.Price);
        }

        [Fact]
        public void CreateProduct_ShouldThrowDomainException_WhenNameIsEmpty()
        {
            // Arrange
            var name = "";
            var price = new ProductPrice(100);

            // Act
            var exception = Record.Exception(() => _productFactory.Create(name, price));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("Product name cannot be null or empty.", exception.Message);
        }
    }
}
