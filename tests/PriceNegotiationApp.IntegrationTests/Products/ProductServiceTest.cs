using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.IntegrationTests.Products
{
    public class ProductServiceTest : BaseIntegrationTest
    {
        private readonly ProductFactory _productFactory;
        private readonly IIdGenerator _idGenMock;
        private readonly ITestOutputHelper _output;

        public ProductServiceTest(
            IntegrationTestFactory testFactory,
            ITestOutputHelper output) : base(testFactory)
        {
            _productFactory = GetService<ProductFactory>();
            _idGenMock = GetService<IIdGenerator>();
            _output = output;
        }

        //[Theory]
        //[InlineData(null, 3)]
        //public async Task GetProducts_WhenProductsExist_ShouldReturnAllRelevantProducts(string? filter, int expectedProductCount)
        //{
        //    // Arrange
        //    var p1 = await SeedProduct("Product A", 9.99m);
        //    var p2 = await SeedProduct("Product B", 12.50m);
        //    var p3 = await SeedProduct("Product C", 100.00m);

        //    // Act
        //    var response = await ProductsClient.GetProductsAsync(filter);
        //    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        //    var result = response.Content.ToList();

        //    Assert.Equal(expectedProductCount, result.Count);
        //    Assert.Contains(result, r => r.Name == p1.Name && r.Price == p1.Price.Value);
        //    Assert.Contains(result, r => r.Name == p2.Name && r.Price == p2.Price.Value);
        //}

        [Theory]
        [InlineData("price lt 20", 2)]
        public async Task GetProducts_WhenProductsExist_ShouldReturnAllRelevantProducts2(string? filter, int expectedProductCount)
        {
            // Arrange
            var p1 = await SeedProduct("Product A", 9.99m);
            var p2 = await SeedProduct("Product B", 12.50m);
            var p3 = await SeedProduct("Product C", 100.00m);

            // Act
            var response = await ProductsClient.GetProductsAsync(filter);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            var result = response.Content.ToList();

            Assert.Equal(expectedProductCount, result.Count);
            Assert.Contains(result, r => r.Name == p1.Name && r.Price == p1.Price.Value);
            Assert.Contains(result, r => r.Name == p2.Name && r.Price == p2.Price.Value);
        }

        [Fact]
        public async Task GetProduct_ShouldReturnSpecifiedProduct()
        {
            // Arrange
            var p1 = await SeedProduct("Product A", 9.99m);

            // Act
            var response = await ProductsClient.GetProductByIdAsync(p1.Id.Value);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

            var result = response.Content;

            // Assert
            Assert.True(result.Id == p1.Id && result.Name == p1.Name && result.Price == p1.Price.Value); // Check if each test data item is present in the returned products
        }

        //[Fact]
        //public async Task GetProduct_ShouldThrowNotFoundExceptionForNonExistingProduct()
        //{

        //    // Act and Assert
        //    await Assert.ThrowsAsync<NotFoundException>(async () =>
        //    {
        //        await productService.GetProductAsync(nonExistingProductId);
        //    });
        //}

        //[Theory]
        //[InlineData("name", 13.37)]
        //[InlineData("a", 0.01)]
        //public async Task CreateProductAsync_ShouldCreateProduct(string name, decimal price)
        //{

        //    // Assert
        //    Assert.NotNull(createdProduct);
        //    Assert.Equal(productInputModel.Name, createdProduct.Name);
        //    Assert.Equal(productInputModel.Price, createdProduct.Price);
        //}

        //[Fact]
        //public async Task DeleteProductAsync_ExistingProduct_ShouldRemoveProduct()
        //{

        //    // Assert
        //    //Assert.True(result);
        //    Assert.DoesNotContain(product, products);
        //}

        //[Fact]
        //public async Task DeleteProductAsync_NonExistingProduct_ShouldNotRemoveProduct()
        //{

        //    // Assert
        //    Assert.False(result);
        //}

        private async Task<Product> SeedProduct(string name, decimal price)
        {
            var product = _productFactory.Create(name, new ProductPrice(price));
            DbContext.Products.Add(product);
            await DbContext.SaveChangesAsync();
            return product;
        }
    }
}