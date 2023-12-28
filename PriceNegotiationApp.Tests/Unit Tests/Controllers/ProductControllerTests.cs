using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Controllers;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceNegotiationApp.Tests.Unit_Tests.Controllers
{
	public class ProductControllerTests
	{
		//[Fact]
		//public void GetProductById_ReturnsProduct()
		//{
		//	// Arrange
		//	string productId = "ba56e8cc-5d4c-475f-8bac-dfb91d780e1e";
		//	var productServiceMock = new Mock<IProductService>();
		//	var loggerMock = new Mock<ILogger<ProductService>>();

		//	var controller = new ProductController(productServiceMock.Object);

		//	// Assume you have a product with ID 1 in your test data
		//	var expectedProduct = new Product { Id = "ba56e8cc-5d4c-475f-8bac-dfb91d780e1e", Name = "TestProduct", Price = 4.50M };

		//	productServiceMock.Setup(x => x.GetProductAsync(productId))
		//					  .ReturnsAsync(expectedProduct);

		//	// Act
		//	var result = controller.GetProduct(productId).GetAwaiter().GetResult();

		//	// Assert
		//	Assert.NotNull(result);
		//	Assert.Equal(expectedProduct, result.Value);
		//}
	}
}
