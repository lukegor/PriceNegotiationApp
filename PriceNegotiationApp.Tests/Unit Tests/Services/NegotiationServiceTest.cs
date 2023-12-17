using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NuGet.Protocol.Core.Types;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
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
		private readonly AppDbContext _dbContext;

		public NegotiationServiceTest(ITestOutputHelper output)
		{
			_output = output;
			_dbContext = DbContextProvider.GetInMemoryDbContext();
		}

		[Fact]
		public async Task GetNegotiations_ShouldReturnAllNegotiations()
		{
			// Arrange
			var negotiationService = CreateNegotiationServiceWithTestData();
			var testData = _dbContext.Negotiations;

			// Act
			var returnedModels = await negotiationService.GetNegotiationsAsync();

			// Assert
			Assert.NotNull(returnedModels);

			var resultList = returnedModels.ToList();
			_output.WriteLine(resultList.Count.ToString());

			// Ensure that the number of returned products matches the number of test data items
			Assert.Equal(testData.Count(), resultList.Count);

			// Check if each test data item is present in the returned products
			foreach (var product in returnedModels)
			{
				Assert.Contains(product, testData);
			}
		}

		[Fact]
		public async Task GetNegotiation_ShouldReturnSpecifiedNegotiation()
		{
			// Arrange
			var negotiationService = CreateNegotiationServiceWithTestData();
			var testData = _dbContext.Negotiations;

			var negotiation = (await negotiationService.GetNegotiationsAsync()).First();

			// Act
			var returnedNegotiation = await negotiationService.GetNegotiationAsync(negotiation.Id);

			// Assert
			Assert.NotNull(returnedNegotiation);

			// Check if each test data item is present in the returned products
			Assert.Contains(returnedNegotiation, testData);
			Assert.Contains(negotiation, testData);
		}

		[Theory]
		[InlineData("123ab", 1.78, "user2")]
		[InlineData("123ac", 1.99, "user3")]
		public async Task CreateNegotiationAsync_ShouldCreateNegotiation(string productId, decimal proposedPrice, string userId)
		{
			// Arrange
			NegotiationInputModel negotiationInputModel = new()
			{
				ProductId = productId,
				ProposedPrice = proposedPrice,
				UserId = userId
			};

			var negotiationService = CreateNegotiationServiceWithTestData(true);
			var testData = _dbContext.Negotiations;

			// Act
			var createdNegotiation = await negotiationService.CreateNegotiationAsync(negotiationInputModel);

			// Assert
			Assert.NotNull(createdNegotiation);
			Assert.Equal(negotiationInputModel.ProductId, createdNegotiation.ProductId);
			Assert.Equal(negotiationInputModel.ProposedPrice, createdNegotiation.ProposedPrice);
			Assert.Equal(negotiationInputModel.UserId, createdNegotiation.UserId);
		}

		public static IEnumerable<object[]> ProvideNegotiationData(AppDbContext dbContext)
		{
			List<object[]> negotiations = new List<object[]>
			{
				new object[] { "123ab", 1.78M, "user2" },
				new object[] { "123ac", 1.99M, "user3" },
			};

			return negotiations;
		}

		private NegotiationService CreateNegotiationServiceWithTestData(bool isCustomProductId = false)
		{
			var context = DbContextProvider.GetInMemoryDbContext();
			PopulateData(context, isCustomProductId);

			// Create a mock for IHttpContextAccessor and set up a basic behavior
			Mock<IHttpContextAccessor> httpAccessorMock = new Mock<IHttpContextAccessor>();
			httpAccessorMock.Setup(x => x.HttpContext)
							.Returns(new DefaultHttpContext());

			return new NegotiationService(context, httpAccessorMock.Object);
		}

		private void PopulateData(AppDbContext dbContext, bool isCustomProductId = false)
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
			dbContext.Negotiations.RemoveRange(dbContext.Negotiations);
			dbContext.Products.RemoveRange(dbContext.Products);
			dbContext.SaveChanges();

			dbContext.Database.EnsureDeleted();
			dbContext.Database.EnsureCreated();

			// Add samples
			if (!isCustomProductId)
			{
				dbContext.Products.AddRange(GetSampleProducts());
			}
			else
			{
				dbContext.Products.AddRange(GetSampleProductsWithCustomIds());
			}
			dbContext.Negotiations.AddRange(GetSampleNegotiations());
			dbContext.SaveChanges();
		}

		private static IEnumerable<Negotiation> GetSampleNegotiations(bool isCustomProductId = false)
		{
			var sampleProducts = GetSampleProducts().ToList(); // Assuming GetSampleProducts is defined
			if (isCustomProductId)
			{
				sampleProducts = GetSampleProductsWithCustomIds().ToList();
			}

			List<Negotiation> negotiations = new List<Negotiation>
			{
				new Negotiation(sampleProducts[0].Id, 4.50M, "user1"),
				new Negotiation(sampleProducts[1].Id, 2.00M, "user2"),
				new Negotiation(sampleProducts[2].Id, 3.00M, "user3"),
			};

			return negotiations;
		}

		private static IEnumerable<Product> GetSampleProducts()
			=> new List<Product>
			{
				new Product{
					//Id = 1,
					Name = "Demo1",
					Price = 5.36M },
				new Product{
					//Id = 2,
					Name = "Demo2",
					Price = 2.36M },
				new Product{
					//Id = 3,
					Name = "Demo3",
					Price = 3.36M },
				new Product{
					//Id = 4,
					Name = "Demo4",
					Price = 4.36M },
				new Product{
					//Id = 5,
					Name = "Demo5",
					Price = 5.36M }
			};

		private static IEnumerable<Product> GetSampleProductsWithCustomIds()
			=> new List<Product>
			{
				new Product{
					//Id = 1,
					Name = "Demo1",
					Price = 5.36M },
				new Product{
					Id = "123ab",
					Name = "Demo2",
					Price = 2.36M },
				new Product{
					Id = "123ac",
					Name = "Demo3",
					Price = 3.36M },
				new Product{
					//Id = 4,
					Name = "Demo4",
					Price = 4.36M },
				new Product{
					//Id = 5,
					Name = "Demo5",
					Price = 5.36M }
			};
	}
}
