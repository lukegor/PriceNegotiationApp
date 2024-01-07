using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using NSubstitute;
using PriceNegotiationApp.Controllers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Tests.Unit_Tests.Services.Fixtures;
using PriceNegotiationApp.Utility;
using PriceNegotiationApp.Utility.Custom_Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PriceNegotiationApp.Tests.Unit_Tests.Services
{
	public class ProductServiceTest: IClassFixture<ProductServiceTestFixture>
	{
		private readonly ITestOutputHelper _output;
        private readonly ProductServiceTestFixture _fixture;

        public ProductServiceTest(ITestOutputHelper output, ProductServiceTestFixture fixture)
		{
			_output = output;
            _fixture = fixture;
        }

		[Fact]
		public async Task GetProducts_ShouldReturnAllProducts()
		{
			// Arrange
			var productService = _fixture.ProductService;
			_fixture.PopulateData(false);

			var testData = _fixture.DbContext.Products.Count();

			// Act
			var returnedModels = await productService.GetProductsAsync();

			// Assert
			Assert.NotNull(returnedModels);
			//var okResult = Assert.IsType<IEnumerable<Product>>(returnedModels);
			Assert.IsAssignableFrom<IEnumerable<Product>>(returnedModels);

			var resultList = returnedModels.ToList();
			_output.WriteLine(resultList.Count().ToString());

            // Ensure that the number of returned products matches the number of test data items
            Assert.Equal(testData, returnedModels.Count());

            // Verify that GetProductsAsync was called once
            //productService.Received(1).GetProductsAsync();

            // Check if each test data item is present in the returned products
            //foreach (var product in testData)
            //{
            //	Assert.Contains(product, resultList);
            //}
        }

		[Fact]
		public async Task GetProduct_ShouldReturnSpecifiedProduct()
		{
			// Arrange
			var productService = _fixture.ProductService;
			_fixture.PopulateData();

            var testData = ProductServiceTestFixture.GetSampleProducts();

			// Act
			var returnedProduct = await productService.GetProductAsync("123abc");

			// Assert
			Assert.NotNull(returnedProduct);
			Assert.Contains(returnedProduct, testData); // Check if each test data item is present in the returned products
		}

		[Fact]
		public async Task GetProduct_ShouldThrowNotFoundExceptionForNonExistingProduct()
		{
			// Arrange
			var productService = _fixture.ProductService;
			_fixture.PopulateData();

            var nonExistingProductId = "nonExistingId";

			// Act and Assert
			await Assert.ThrowsAsync<NotFoundException>(async () =>
			{
				await productService.GetProductAsync(nonExistingProductId);
			});
		}

		[Theory]
		[InlineData("name", 13.37)]
		[InlineData("", 0.01)]
		public async Task CreateProductAsync_ShouldCreateProduct(string name, decimal price)
		{
			// Arrange
			var productInputModel = new ProductInputModel
			{
				Name = name,
				Price = price
			};

			using var context = DbContextProvider.GetInMemoryDbContext();
			var fakeLogger = Substitute.For<ILogger<ProductService>>();

			var productService = new ProductService(context, fakeLogger);

			// Act
			var createdProduct = await productService.CreateProductAsync(productInputModel);

			// Assert
			Assert.NotNull(createdProduct);
			Assert.Equal(productInputModel.Name, createdProduct.Name);
			Assert.Equal(productInputModel.Price, createdProduct.Price);
		}

		[Fact]
		public async Task DeleteProductAsync_ExistingProduct_ShouldRemoveProduct()
		{
			// Arrange
			var productService = _fixture.ProductService;
            _fixture.PopulateData();

            string randomId = "123abc";

			foreach (var prooduct in await productService.GetProductsAsync())
			{
				_output.WriteLine(prooduct.Id);
			}

			var product = await productService.GetProductAsync(randomId);

			// Act
			bool result = await productService.DeleteProductAsync(randomId);

			var products = await productService.GetProductsAsync();

			// Assert
			Assert.True(result);
			Assert.DoesNotContain(product, products);
		}

		[Fact]
		public async Task DeleteProductAsync_NonExistingProduct_ShouldNotRemoveProduct()
		{
			// Arrange
			var productService = _fixture.ProductService;
			string nonExistingProductId = Guid.NewGuid().ToString();

			// Act
			bool result = await productService.DeleteProductAsync(nonExistingProductId);

			// Assert
			Assert.False(result);
		}
	}
}