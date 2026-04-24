using Bogus;
using FluentAssertions;
using NSubstitute;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Products
{
    public class UpdateShould
    {
        private readonly ProductFactory _productFactory;
        private readonly IIdGenerator _idGenerator;
        private readonly Faker _faker = new("pl");

        public UpdateShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _productFactory = new ProductFactory(_idGenerator);
        }

        [Fact]
        public void UpdateProduct_WithValidData_ShouldUpdateSuccessfully()
        {
            // Arrange
            var expectedProductId = _faker.Random.Guid();
            var productName = _faker.Commerce.ProductName();
            var amount = _faker.Finance.Amount(1, 1000000);
            _idGenerator.NewId().Returns(expectedProductId);

            var product = _productFactory.Create(productName, new ProductPrice(amount));

            var expected = new
            {
                Id = ProductId.From(expectedProductId),
                Name = _faker.Commerce.ProductName(),
                Price = new ProductPrice(_faker.Finance.Amount(1, 1000000))
            };

            // Act
            product.Update(expected.Name, expected.Price);

            // Assert
            product.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void UpdateProduct_ShouldThrowDomainException_WhenNameIsEmpty()
        {
            // Arrange
            var product = _productFactory.Create("Test Product", new ProductPrice(100));
            var name = "";
            var price = new ProductPrice(100);

            // Act
            var exception = Record.Exception(() => product.Update(name, price));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("Product name cannot be null or empty.", exception.Message);
        }
    }
}
