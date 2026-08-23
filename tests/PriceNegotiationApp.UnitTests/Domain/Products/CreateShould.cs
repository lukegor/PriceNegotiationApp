using Bogus;

using FluentAssertions;

using NSubstitute;

namespace PriceNegotiationApp.UnitTests.Domain.Products
{
    public class CreateShould
    {
        private readonly ProductFactory _productFactory;
        private readonly IIdGenerator _idGenerator;
        private readonly Faker _faker = new("pl");

        public CreateShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _productFactory = new ProductFactory(_idGenerator);
        }

        [Fact]
        public void CreateProduct_WithValidData()
        {
            // Arrange
            var product = ProductBuilder.Default();

            _idGenerator.NewId().Returns(product.Id.Value);

            var expected = new Product(ProductId.From(product.Id.Value), product.Name, product.Price);

            // Act
            var result = _productFactory.Create(product.Name, product.Price);

            // Assert
            result.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void CreateProduct_ShouldThrowDomainException_WhenNameIsEmpty(string? name)
        {
            // Arrange
            var price = new ProductPrice(_faker.Finance.Amount(1, 10000));

            // Act
            var exception = Record.Exception(() => _productFactory.Create(name, price));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("Product name cannot be null or empty.", exception.Message);
        }
    }
}
