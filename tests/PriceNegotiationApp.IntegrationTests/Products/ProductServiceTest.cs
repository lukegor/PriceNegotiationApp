using Bogus;
using FluentAssertions;
using PriceNegotiationApp.Contracts.Products.Dtos.Requests;
using PriceNegotiationApp.Domain.Models.Products;
using System.Net;

namespace PriceNegotiationApp.IntegrationTests.Products
{
    public class ProductServiceTest : BaseIntegrationTest
    {
        private readonly ProductFactory _productFactory;
        private readonly ITestOutputHelper _output;

        private readonly Faker _faker = new("pl");

        public ProductServiceTest(
            IntegrationTestFactory testFactory,
            ITestOutputHelper output) : base(testFactory)
        {
            _productFactory = GetService<ProductFactory>();
            _output = output;
        }

        [Theory]
        [InlineData(null, 3)]
        [InlineData("price gt 20", 2)]
        public async Task GetProducts_ShouldReturnAllRelevantProducts_WhenProductsExist(string? filter, int expectedProductCount)
        {
            // Arrange
            var pBelow = SeedProduct(_faker.Commerce.ProductName(), _faker.Finance.Amount(1, 19));
            var pAbove1 = SeedProduct(_faker.Commerce.ProductName(), _faker.Finance.Amount(21));
            var pAbove2 = SeedProduct(_faker.Commerce.ProductName(), _faker.Finance.Amount(21));

            var allProducts = new[] { pBelow, pAbove1, pAbove2 };
            var expected = allProducts
                .TakeLast(expectedProductCount)
                .Select(p => p.ToExpectedDto());

            // Act
            var response = await AsGuest<IProductsApi>().GetProductsAsync(filter);

            // Assert
            var result = response.Content.ToList();
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expectedProductCount, result.Count);
            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task GetProduct_ShouldReturnSpecifiedProduct_WhenProductExists()
        {
            // Arrange
            var p1 = SeedProduct(_faker.Commerce.ProductName(), _faker.Finance.Amount());

            // Act
            var response = await AsGuest<IProductsApi>().GetProductByIdAsync(p1.Id.Value);

            // Assert
            var result = response.Content;
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            result.Should().BeEquivalentTo(p1.ToExpectedDto());
        }

        [Fact]
        public async Task GetProduct_WhenNonExistingProduct_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistingId = _faker.Random.Guid();

            // Act
            var response = await AsAdmin<IProductsApi>().GetProductByIdAsync(nonExistingId);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateProductAsync_ShouldCreateProduct_AsAdmin()
        {
            // Arrange
            var request = new ProductRequestDto
            {
                Name = _faker.Commerce.ProductName(),
                Price = _faker.Finance.Amount()
            };

            // Act
            var response = await AsAdmin<IProductsApi>().CreateProductAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            request.ToExpectedDto().Should().BeEquivalentTo(response.Content, options => options
                .Excluding(ctx => ctx.Path == nameof(Product.Id)));
        }

        [Theory]
        [InlineData("User", HttpStatusCode.Forbidden)]
        [InlineData("Guest", HttpStatusCode.Unauthorized)]
        public async Task CreateProduct_ShouldReturnCorrectErrorCode_ForConcernedRolesWhenAccessIsRestricted(string role, HttpStatusCode expected)
        {
            // Arrange
            var request = new ProductRequestDto
            {
                Name = _faker.Commerce.ProductName(),
                Price = _faker.Finance.Amount()
            };

            var client = GetClientForRole<IProductsApi>(role);

            // Act
            var response = await client.CreateProductAsync(request);

            // Assert
            Assert.Equal(expected, response.StatusCode);
        }

        [Fact]
        public async Task UpdateProduct_ShouldUpdate_AsAdmin()
        {
            // Arrange
            var product = SeedProduct(_faker.Commerce.ProductName(), _faker.Finance.Amount(1, 10000000));
            var request = new ProductRequestDto { Name = _faker.Commerce.ProductName(), Price = _faker.Finance.Amount(1, 10000000) };

            // Act
            var response = await AsAdmin<IProductsApi>().UpdateProductAsync(product.Id.Value, request);
            var result = response.Content;

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            request.ToExpectedDto().Should().BeEquivalentTo(result, options =>
                options.Excluding(p => p.Path == nameof(Product.Id)));
        }

        [Fact]
        public async Task UpdateProduct_ShouldReturnNotFound_WhenProductDoesNotExist()
        {
            // Arrange
            var nonExistingId = _faker.Random.Guid();
            var request = new ProductRequestDto { Name = _faker.Commerce.ProductName(), Price = _faker.Finance.Amount(1, 10000000) };

            // Act
            var response = await AsAdmin<IProductsApi>().UpdateProductAsync(nonExistingId, request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Theory]
        [InlineData("User", HttpStatusCode.Forbidden)]
        [InlineData("Guest", HttpStatusCode.Unauthorized)]
        public async Task UpdateProduct_ShouldReturnCorrectErrorCode_ForConcernedRolesWhenAccessIsRestricted(string role, HttpStatusCode expected)
        {
            // Arrange
            var client = GetClientForRole<IProductsApi>(role);
            var request = new ProductRequestDto { Name = _faker.Commerce.ProductName(), Price = _faker.Finance.Amount(1, 10000000) };

            // Act
            var response = await client.UpdateProductAsync(Guid.NewGuid(), request);

            // Assert
            response.StatusCode.Should().Be(expected);
        }

        [Fact]
        public async Task DeleteProductAsync_ShouldRemoveProduct_WhenValidConditions()
        {
            // Arrange
            var product = SeedProduct(_faker.Commerce.ProductName(), _faker.Finance.Amount());

            // Act
            var response = await AsAdmin<IProductsApi>().DeleteProductAsync(product.Id.Value);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.DoesNotContain(product, DbContext.Products);
        }

        [Fact]
        public async Task DeleteProductAsync_ShouldNotRemoveProduct_WhenNonExistingProduct()
        {
            // Arrange
            var nonExistingId = _faker.Random.Guid();

            // Act
            var response = await AsAdmin<IProductsApi>().DeleteProductAsync(nonExistingId);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("User", HttpStatusCode.Forbidden)]
        [InlineData("Guest", HttpStatusCode.Unauthorized)]
        public async Task DeleteProduct_ReturnsExpectedStatus_ForConcernedRolesWhenAccessIsRestricted(string role, HttpStatusCode expected)
        {
            // Act
            var response = await GetClientForRole<IProductsApi>(role).DeleteProductAsync(Guid.NewGuid());

            // Assert
            response.StatusCode.Should().Be(expected);
        }
    }
}