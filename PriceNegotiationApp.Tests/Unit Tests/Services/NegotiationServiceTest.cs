using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NuGet.Protocol.Core.Types;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Models.Input_Models;
using PriceNegotiationApp.Services;
using PriceNegotiationApp.Services.Providers;
using PriceNegotiationApp.Tests.Unit_Tests.Services.Fixtures;
using PriceNegotiationApp.Utility;
using PriceNegotiationApp.Utility.Custom_Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace PriceNegotiationApp.Tests.Unit_Tests.Services
{
	public class NegotiationServiceTest : IClassFixture<NegotiationServiceTestFixture>
    {
		private readonly ITestOutputHelper _output;
        private readonly NegotiationServiceTestFixture _fixture;

        public NegotiationServiceTest(ITestOutputHelper output, NegotiationServiceTestFixture fixture)
		{
			_output = output;
			_fixture = fixture;
		}

		[Fact]
		public async Task GetNegotiations_ShouldReturnAllNegotiations()
		{
			// Arrange
			var negotiationService = _fixture.NegotiationService;
			_fixture.PopulateData();

			var testData = _fixture.DbContext.Negotiations;

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
			var negotiationService = _fixture.NegotiationService;
			_fixture.PopulateData();

			var testData = _fixture.DbContext.Negotiations;

			var negotiation = (await negotiationService.GetNegotiationsAsync()).First();

			// Act
			var returnedNegotiation = await negotiationService.GetNegotiationAsync(negotiation.Id);

			// Assert
			Assert.NotNull(returnedNegotiation);

			// Check if each test data item is present in the returned products
			Assert.Contains(returnedNegotiation, testData);
			Assert.Contains(negotiation, testData);
		}

        [Fact]
        public async Task GetNegotiation_ShouldThrowNotFoundException()
        {
            // Arrange
            var negotiationService = _fixture.NegotiationService;
            _fixture.PopulateData();

            var nonExistingNegotiationId = new Random().Next(1000000000);

			// Act and Assert
			await Assert.ThrowsAsync<NotFoundException>(async () =>
			{
				await negotiationService.GetNegotiationAsync(nonExistingNegotiationId);
			});
        }

        [Theory]
		[InlineData("123ab", 1.78/*, "user2"*/)]
		[InlineData("123ac", 1.99/*, "user3"*/)]
		public async Task CreateNegotiationAsync_ShouldCreateNegotiation(string productId, decimal proposedPrice/*, string userId*/)
		{
			// Arrange

			// Create a ClaimsPrincipal with the desired user
			//ClaimsIdentity claimsIdentity = new ClaimsIdentity(new Claim[]
			//{
			//	new Claim(ClaimTypes.Name, userId),
			//});

			//ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

			//// Inject the claimsPrincipal into IClaimsProvider
			//var claimsProviderMock = new Mock<IClaimsProvider>();
			//claimsProviderMock.Setup(cp => cp.UserClaimsPrincipal).Returns(claimsPrincipal);

			var negotiationService = _fixture.NegotiationService;
            _fixture.PopulateData(true);

			var testData = _fixture.DbContext.Negotiations;

			NegotiationInputModel negotiationInputModel = new()
			{
				ProductId = productId,
				ProposedPrice = proposedPrice,
			};

			// Act
			var createdNegotiation = await negotiationService.CreateNegotiationAsync(negotiationInputModel);

			// Assert
			Assert.NotNull(createdNegotiation);
			Assert.Equal(negotiationInputModel.ProductId, createdNegotiation.ProductId);
			Assert.Equal(negotiationInputModel.ProposedPrice, createdNegotiation.ProposedPrice);
		}

		//[Theory]
		//[InlineData("123ab", 20, "")]
		//[InlineData("", 20, "user2")]
		//public async Task CreateNegotiationAsync_ShouldNotCreateNegotiation(string productId, decimal proposedPrice, string userId)
		//{
		//	// Arrange
		//	var negotiationService = CreateNegotiationServiceWithTestData(true, userId);
		//	var testData = _dbContext.Negotiations;

		//	NegotiationInputModel negotiationInputModel = new()
		//	{
		//		ProductId = productId,
		//		ProposedPrice = proposedPrice,
		//	};

		//	// Act
		//	var createdNegotiation = await negotiationService.CreateNegotiationAsync(negotiationInputModel);

		//	// Assert
		//	Assert.Null(createdNegotiation);
		//	//Assert.Equal(negotiationInputModel.ProductId, createdNegotiation.ProductId);
		//	//Assert.Equal(negotiationInputModel.ProposedPrice, createdNegotiation.ProposedPrice);
		//}

		[Theory]
		//[InlineData(true, NegotiationStatus.Closed, 0)]
		[InlineData(false, NegotiationStatus.Open, 1)]
		public async Task RespondToNegotiationProposalAsync_ShouldUpdateNegotiation(bool isApproved, NegotiationStatus expectedStatus, int expectedRetries)
		{
			// Arrange
			var negotiationService = _fixture.NegotiationService;
			_fixture.PopulateData();

			var existingNegotiation = (await negotiationService.GetNegotiationsAsync()).First();

			// Act
			var result = await negotiationService.RespondToNegotiationProposalAsync(existingNegotiation.Id, isApproved);

			// Assert
			Assert.Equal(UpdateResultType.Success, result);
			Assert.Equal(expectedStatus, existingNegotiation.Status);
			//Assert.Equal(expectedRetries, existingNegotiation.RetriesLeft);
		}

		[Fact]
		public async Task DeleteNegotiationAsync_NonExistingNegotiation_ShouldNotRemoveProduct()
		{
			// Arrange
			var negotiationService = _fixture.NegotiationService;
			_fixture.PopulateData();

			int nonExistingNegotiationId = new Random().Next(1000000000);

			// Act
			bool result = await negotiationService.DeleteNegotiationAsync(nonExistingNegotiationId);

			// Assert
			Assert.False(result);
		}
	}
}
