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
			var productService = _fixture.CreateProductServiceWithTestData(false);
			//var testData = productService.;

			// Act
			var returnedModels = await productService.GetProductsAsync();

			// Assert
			Assert.NotNull(returnedModels);
			//var okResult = Assert.IsType<IEnumerable<Product>>(returnedModels);
			Assert.IsAssignableFrom<IEnumerable<Product>>(returnedModels);

			var resultList = returnedModels.ToList();
			_output.WriteLine(resultList.Count().ToString());

			// Ensure that the number of returned products matches the number of test data items
			//Assert.Equal(testData.Count(), resultList.Count);

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
			var productService = _fixture.CreateProductServiceWithTestData();
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
			var productService = _fixture.CreateProductServiceWithTestData();
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

			var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase(databaseName: "InMemoryDatabase")
				.Options;

			using var context = DbContextProvider.GetInMemoryDbContext();
			var fakeLogger = Substitute.For<ILogger<ProductService>>();


			// Initialize the service with the in-memory DbContext
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
			var productService = _fixture.CreateProductServiceWithTestData();

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
			var productService = _fixture.CreateProductServiceWithTestData();
			string nonExistingProductId = Guid.NewGuid().ToString();

			// Act
			bool result = await productService.DeleteProductAsync(nonExistingProductId);

			// Assert
			Assert.False(result);
		}

		//static int ChooseRandomId<Product>(IEnumerable<Product> items)
		//{
		//	// Check if the IEnumerable is empty
		//	if (items == null || !items.Any())
		//	{
		//		throw new ArgumentException("The IEnumerable is empty");
		//	}

		//	// Create a Random object
		//	Random random = new Random();

		//	// Generate a random index within the range of the number of elements
		//	int randomIndex = random.Next(1, items.Count());

		//	// Return the random index
		//	return randomIndex;
		//}

		//private readonly ITestOutputHelper _output;

		//public ProductTest(ITestOutputHelper output)
		//{
		//	_output = output;
		//}

		//[Fact]
		//public async void AllVisits_ReturnsOkResult_WhenVisitsExist()
		//{
		//	var options = new DbContextOptionsBuilder<AppDbContext>()
		//   .UseInMemoryDatabase(databaseName: "x")
		//   .Options;

		//	using (var dbContext = new AppDbContext(options))
		//	{
		//		dbContext.ApplicationUsers.Add(new ApplicationUser
		//		{
		//			Id = "testUserId",
		//			Name = "someName",
		//			Role = Roles.Role_Customer
		//		});
		//		dbContext.SaveChanges();
		//		// Arrange
		//		var mockService = new Mock<ProductService>(dbContext);
		//		var claims = new List<Claim>()
		//		{
		//			new Claim(ClaimTypes.NameIdentifier, "testUserId"),
		//		};
		//		var identity = new ClaimsIdentity(claims, "TestAuthType");
		//		var principal = new ClaimsPrincipal(identity);

		//		var mockControllerContext = new Mock<ControllerContext>();
		//		mockControllerContext.Object.HttpContext = new DefaultHttpContext();

		//		var controller = new ProductController(mockService.Object)
		//		{
		//			ControllerContext = mockControllerContext.Object
		//		};


		//		// Act
		//		var result = await controller.GetProducts();

		//		// Assert
		//		var okResult = Assert.IsType<ActionResult<IEnumerable<Product>>>(result);
		//		Assert.IsAssignableFrom<IEnumerable<Product>>(okResult.Value);
		//	}
		//}
	}
}


/*
using Microsoft.AspNetCore.Mvc;
using Moq;
using PriceNegotiationApp.Controllers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace PriceNegotiationApp.Tests.Tests.Public_Api_Tests.ProductEndpoints
{
	public class ProductTest
	{
		private readonly ITestOutputHelper _output;

		public ProductTest(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public async Task ProductServiceTest()
		{
			// Arrange
			var mockProductService = new Mock<IProductService>();
			var expectedProducts = GetTestProducts();

			// Ensure that the setup is correctly configured
			mockProductService.Setup(service => service.GetProductsAsync()).ReturnsAsync(expectedProducts);

			var controller = new ProductController(mockProductService.Object);

			// Act
			var result = await controller.GetProducts();

			// Assert
			var okResult = Assert.IsType<ActionResult<IEnumerable<Product>>>(result);

			_output.WriteLine($"ActionResult Value Type: {okResult.Value?.GetType().FullName}");

			if (okResult.Value == null)
			{
				// Print more information if the value is null
				_output.WriteLine("ActionResult Value is NULL. Mock Setup Details:");

				// Print all setups on the mock
				foreach (var setup in mockProductService.Setups)
				{
					_output.WriteLine($"Setup: {setup}");
				}
			}

			// Ensure that the value is not null
			Assert.NotNull(okResult.Value);

			// Ensure that the value is of the expected type
			var products = Assert.IsAssignableFrom<IEnumerable<Product>>(okResult.Value);

			// Ensure that the expected number of products is returned
			Assert.Equal(4, products.Count());

			// Verify that GetProductsAsync was called once
			mockProductService.Verify(service => service.GetProductsAsync(), Times.Once);
		}

		private List<Product> GetTestProducts()
		{
			var testProducts = new List<Product>();
			testProducts.Add(new Product { Name = "Demo1", Price = 1 });
			testProducts.Add(new Product { Name = "Demo2", Price = 3.75M });
			testProducts.Add(new Product { Name = "Demo3", Price = 16.99M });
			testProducts.Add(new Product { Name = "Demo4", Price = 11.00M });

			return testProducts;
		}
	}
}

*/