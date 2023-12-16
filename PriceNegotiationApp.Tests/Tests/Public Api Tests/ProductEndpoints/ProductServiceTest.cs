using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Moq;
using PriceNegotiationApp.Controllers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PriceNegotiationApp.Tests.Tests.Public_Api_Tests.ProductEndpoints
{
	public static class DbContextProvider
	{
		public static AppDbContext GetInMemoryDbContext() =>
			new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase("Tests").Options);
	}

	public class ProductServiceTest
	{
		private readonly ITestOutputHelper _output;

		public ProductServiceTest(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public async Task GetProducts_ShouldReturnAllProducts()
		{
			// Arrange
			var productService = CreateProductServiceWithTestData();
			var testData = GetSampleProducts();

			// Act
			var returnedModels = await productService.GetProductsAsync();

			// Assert
			Assert.NotNull(returnedModels);

			var resultList = returnedModels.ToList();
			_output.WriteLine(resultList.Count().ToString());

			// Ensure that the number of returned products matches the number of test data items
			Assert.Equal(testData.Count(), resultList.Count);

			// Check if each test data item is present in the returned products
			foreach (var product in testData)
			{
				//_output.WriteLine($"{product.GetType().ToString()} {resultList.ElementAt(counter)}");
				//_output.WriteLine($"{product.Name} {resultList.ElementAt(counter).Name}");
				Assert.Contains(product, resultList);
			}
		}

		[Fact]
		public async Task GetProduct_ShouldReturnSpecifiedProduct()
		{
			// Arrange
			var productService = CreateProductServiceWithTestData();
			var testData = GetSampleProducts();

			//int randomId = ChooseRandomId(testData);

			foreach (var product in testData)
			{
				_output.WriteLine(product.Id.ToString());
			}

			var productExists = await productService.GetProductsAsync();
			var productExists2 = productExists.ToList().Any(p => p.Id == testData.First().Id);
			if (!productExists2)
			{
				_output.WriteLine("XD");
			}

			// Act
			//_output.WriteLine($"index: {randomId.ToString()}");
			//_output.WriteLine($"item: {testData.ElementAt(randomId-1).Id.ToString()}");
			var returnedProduct = await productService.GetProductAsync(testData.First().Id);

			// Assert
			Assert.NotNull(returnedProduct);

			// Check if each test data item is present in the returned products
			Assert.Contains(returnedProduct, testData);
		}

		private IProductService CreateProductServiceWithTestData()
		{
			var context = DbContextProvider.GetInMemoryDbContext();
			PopulateData(context);
			return new ProductService(context);
			//var mockProductService = new Mock<IProductService>();
			//mockProductService.Setup(x => x.GetProductsAsync()).ReturnsAsync(GetSampleProducts());
			//return mockProductService.Object;
		}

		private void PopulateData(AppDbContext dbContext)
		{
			dbContext.Products.AddRange(GetSampleProducts());
			dbContext.SaveChanges();
		}

		private IEnumerable<Product> GetSampleProducts()
			=> new List<Product>
			{
				new Product{
					Id = 1,
					Name = "Demo1",
					Price = 5.36M },
				new Product{
					Id = 2,
					Name = "Demo2",
					Price = 2.36M },
				new Product{
					Id = 3,
					Name = "Demo3",
					Price = 3.36M },
				new Product{
					Id = 4,
					Name = "Demo4",
					Price = 4.36M },
				new Product{
					Id = 5,
					Name = "Demo5",
					Price = 5.36M }
			};

		static int ChooseRandomId<Product>(IEnumerable<Product> items)
		{
			// Check if the IEnumerable is empty
			if (items == null || !items.Any())
			{
				throw new ArgumentException("The IEnumerable is empty");
			}

			// Create a Random object
			Random random = new Random();

			// Generate a random index within the range of the number of elements
			int randomIndex = random.Next(1, items.Count());

			// Return the random index
			return randomIndex;
		}

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