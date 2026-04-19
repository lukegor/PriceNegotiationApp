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

        public UpdateShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _productFactory = new ProductFactory(_idGenerator);
        }

        [Fact]
        public void UpdateProduct_WithValidData_ShouldUpdateSuccessfully()
        {
            // Arrange
            var expectedProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            _idGenerator.NewId().Returns(expectedProductId);

            var product = _productFactory.Create("Laptop", new ProductPrice(100));

            var expected = new
            {
                Id = ProductId.From(expectedProductId),
                Name = "Laptop Lenovo",
                Price = new ProductPrice(200)
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

        [Fact]
        public void UpdateProduct_ShouldThrowDomainException_WhenNoChangesDetected()
        {
            // Arrange
            var price = new ProductPrice(100);
            var product = _productFactory.Create("Laptop", price);

            // Act
            var exception = Record.Exception(() =>
                product.Update("Laptop", price));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("No changes detected", exception.Message);
        }

        [Fact]
        public void HasChanges_ReturnFalse_WhenNameAndPriceMatch()
        {
            // Arrange
            var price = new ProductPrice(100);
            var product = _productFactory.Create("Laptop", price);

            // Act
            var hasChanges = product.HasChanges("Laptop", price);

            // Assert
            Assert.False(hasChanges);
        }

        [Fact]
        public void HasChanges_ReturnTrue_WhenNameOrPriceDiffers()
        {
            // Arrange
            var product = _productFactory.Create("Laptop", new ProductPrice(100));

            // Act
            var changedName = product.HasChanges("Laptop Pro", new ProductPrice(100));
            var changedPrice = product.HasChanges("Laptop", new ProductPrice(120));

            // Assert
            Assert.True(changedName);
            Assert.True(changedPrice);
        }
    }
}
