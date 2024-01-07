using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PriceNegotiationApp.Controllers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;
using PriceNegotiationApp.Utility.Custom_Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace PriceNegotiationApp.Tests.Unit_Tests.Controllers
{
	public class ProductControllerTests
	{
        private readonly ITestOutputHelper _output;

        public ProductControllerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetProducts_ShouldReturnListOfProducts()
        {
            // Arrange
            var productServiceSubstitute = Substitute.For<IProductService>();
            var products = new List<Product>
            {
                new Product { Id = "123", Name = "Product1" },
                new Product { Id = "321", Name = "Product2" }
                // Add more products as needed
            };

            productServiceSubstitute.GetProductsAsync().Returns(Task.FromResult<IEnumerable<Product>>(products));

            var productController = new ProductController(productServiceSubstitute);

            // Act
            var result = await productController.GetProducts();

            // Assert
            var okObjectResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okObjectResult.StatusCode);

            var returnedProducts = Assert.IsType<List<Product>>(okObjectResult.Value);
            Assert.Equal(products, returnedProducts);
        }

        [Fact]
        public async void GetProductById_ShouldReturnProduct()
        {
            // Arrange
            string productId = "ba56e8cc-5d4c-475f-8bac-dfb91d780e1e";

            var loggerMock = Substitute.For<ILogger<ProductService>>();
            var productServiceMock = Substitute.For<IProductService>();

            var controller = new ProductController(productServiceMock);

            // Assume you have a product such product in your test data
            var expectedProduct = new Product { Id = "ba56e8cc-5d4c-475f-8bac-dfb91d780e1e", Name = "TestProduct", Price = 4.50M };

            productServiceMock.GetProductAsync(productId)
                .Returns(Task.FromResult(expectedProduct));

            // Act
            var result = await controller.GetProduct(productId);

            // Assert
            Assert.NotNull(result);

            var okObjectResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, okObjectResult.StatusCode);

            var returnedProduct = Assert.IsType<Product>(okObjectResult.Value);
            Assert.Equal(expectedProduct.Id, returnedProduct.Id);
            Assert.Equal(expectedProduct.Name, returnedProduct.Name);
            Assert.Equal(expectedProduct.Price, returnedProduct.Price);
        }

        [Fact]
        public async void GetProductById_ShouldReturnNotFoundStatusCode()
        {
            // Arrange
            string productId = "nonExistentId";

            var productServiceMock = Substitute.For<IProductService>();

            var controller = new ProductController(productServiceMock);

            productServiceMock.GetProductAsync(productId)
                .Throws(new NotFoundException());

            // Act
            var result = await controller.GetProduct(productId);

            // Assert
            Assert.NotNull(result);

            var notFoundResult = Assert.IsType<NotFoundResult>(result.Result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task PostProduct_ShouldCreateProduct()
        {
            // Arrange
            string productName = "Item";
            decimal price = 50;

            ProductInputModel productInputModel = new ProductInputModel()
            {
                Name = productName,
                Price = price
            };

            var productServiceMock = Substitute.For<IProductService>();
            productServiceMock.CreateProductAsync(Arg.Any<ProductInputModel>())
                .Returns(Task.FromResult(new Product { Name = productName, Price = price }));

            var controller = new ProductController(productServiceMock);
            controller.ModelState.Clear();

            // Act
            var result = await controller.PostProduct(productInputModel);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdAtActionResult.StatusCode);
        }

        [Fact]
        public async Task DeleteProduct_ProductExists_ReturnsNoContent()
        {
            // Arrange
            string productId = "123";
            var productServiceMock = Substitute.For<IProductService>();
            productServiceMock.DeleteProductAsync(productId)
                .Returns(Task.FromResult(true)); // Simulate successful deletion

            var controller = new ProductController(productServiceMock);

            // Act
            var result = await controller.DeleteProduct(productId);

            // Assert
            var noContentResult = Assert.IsType<NoContentResult>(result);
            Assert.Equal(204, noContentResult.StatusCode);
        }

        [Fact]
        public async Task DeleteProduct_ProductDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string productId = "nonexistent-id";
            var productServiceMock = Substitute.For<IProductService>();
            productServiceMock.DeleteProductAsync(productId)
                .Returns(Task.FromResult(false)); // Simulate deletion failure

            var controller = new ProductController(productServiceMock);

            // Act
            var result = await controller.DeleteProduct(productId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task PutProduct_ShouldUpdateProduct()
        {
            // Arrange
            string productId = "123";
            var product = new Product { Name = "Keyboard", Price = 100 };
            var productServiceMock = Substitute.For<IProductService>();
            productServiceMock.UpdateProductAsync(productId, product)
                .Returns(Task.FromResult(Utility.UpdateResultType.Success));

            var controller = new ProductController(productServiceMock);
            controller.ModelState.Clear();

            // Act
            var result = await controller.PutProduct(productId, product);

            // Assert
            var noContentResult = Assert.IsType<NoContentResult>(result);
            Assert.Equal(204, noContentResult.StatusCode);
        }

        [Fact]
        public async Task PutProduct_ProductNotFound_ReturnsNotFound()
        {
            // Arrange
            string nonExistingProductId = "nonexistent-id";
            var invalidProduct = new Product { Name = "Keyboard", Price = 100 };
            var productServiceMock = Substitute.For<IProductService>();
            productServiceMock.UpdateProductAsync(nonExistingProductId, invalidProduct)
                .Returns(Task.FromResult(UpdateResultType.NotFound));

            var controller = new ProductController(productServiceMock);

            // Act
            var result = await controller.PutProduct(nonExistingProductId, invalidProduct);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task PutProduct_ConcurrencyConflict_ReturnsBadRequest()
        {
            // Arrange
            string productId = "123";
            var conflictingProduct = new Product { Name = "Keyboard", Price = 100 };
            var productServiceMock = Substitute.For<IProductService>();
            productServiceMock.UpdateProductAsync(productId, conflictingProduct)
                .Returns(Task.FromResult(UpdateResultType.Conflict));

            var controller = new ProductController(productServiceMock);

            // Act
            var result = await controller.PutProduct(productId, conflictingProduct);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflictResult.StatusCode);
            Assert.Equal("Concurrency conflict", conflictResult.Value);
        }
    }
}
