using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace PriceNegotiationApp.Tests.Unit_Tests.Services
{
	public class NegotiationServiceTest
	{
		private readonly ITestOutputHelper _output;

		public NegotiationServiceTest(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public async Task GetNegotiations_ShouldReturnAllNegotiations()
		{
			// Arrange
			var productService = CreateProductServiceWithTestData();
			var testData = GetSampleNegotiations();

			// Act
			var returnedModels = await productService.GetNegotiationsAsync();

			// Assert
			Assert.NotNull(returnedModels);

			var resultList = returnedModels.ToList();
			_output.WriteLine(resultList.Count.ToString());

			// Ensure that the number of returned products matches the number of test data items
			Assert.Equal(testData.Count(), resultList.Count);

			// Check if each test data item is present in the returned products
			foreach (var product in testData)
			{
				Assert.Contains(product, resultList);
			}
		}

		private NegotiationService CreateProductServiceWithTestData()
		{
			var context = DbContextProvider.GetInMemoryDbContext();
			PopulateData(context);

			// Create a mock for IHttpContextAccessor and set up a basic behavior
			Mock<IHttpContextAccessor> httpAccessorMock = new Mock<IHttpContextAccessor>();
			httpAccessorMock.Setup(x => x.HttpContext)
							.Returns(new DefaultHttpContext());

			return new NegotiationService(context, httpAccessorMock.Object);
		}

		private void PopulateData(AppDbContext dbContext)
		{
			//dbContext.Products.Load();
			//foreach (var existingProduct in dbContext.Products)
			//{
			//	_output.WriteLine($"Existing Product ID: {existingProduct.Id}, Name: {existingProduct.Name}");
			//}
			//var sampleProducts = dbContext.Products.ToList();
			//var sampleNegotiations = GetSampleNegotiations().ToList();

			//_output.WriteLine($"Sample Products Count: {sampleProducts.Count}");
			//_output.WriteLine($"Sample Negotiations Count: {sampleNegotiations.Count}");


			//// Clear existing data
			//dbContext.Negotiations.RemoveRange(dbContext.Negotiations);
			//dbContext.Products.RemoveRange(dbContext.Products);
			//dbContext.SaveChanges();

			dbContext.Database.EnsureDeleted();
			dbContext.Database.EnsureCreated();

			var sampleProducts2 = dbContext.Products.ToList(); ;
			var sampleNegotiations2 = GetSampleNegotiations().ToList();

			_output.WriteLine($"Sample Products Count: {sampleProducts2.Count}");
			_output.WriteLine($"Sample Negotiations Count: {sampleNegotiations2.Count}");

			// Add samples
			dbContext.Products.AddRange(GetSampleProducts());
			dbContext.Negotiations.AddRange(GetSampleNegotiations());
			dbContext.SaveChanges();
		}

		private static IEnumerable<Negotiation> GetSampleNegotiations()
			=> new List<Negotiation>
			{
				//new Negotiation(1, 5M, )
			};

		private static IEnumerable<Product> GetSampleProducts()
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
	}
}
