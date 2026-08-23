using Bogus;
using PriceNegotiationApp.Domain.Models.Negotiations;
using System.Net;

namespace PriceNegotiationApp.IntegrationTests.Negotiations
{
    [Obsolete("TODO: Redo")]
    public class NegotiationServiceTest : BaseIntegrationTest, IClassFixture<IntegrationTestFactory>
    {
        private readonly NegotiationFactory _negotiationFactory;
        private readonly ITestOutputHelper _output;

        private readonly Faker _faker = new("pl");

        public NegotiationServiceTest(IntegrationTestFactory testFactory,
            ITestOutputHelper output) : base(testFactory)
        {
            _output = output;
            _negotiationFactory = GetService<NegotiationFactory>();
        }

        // TODO: Differentiate not only test but also the OData DTO structure between admin and customer
        [Fact]
        public async Task GetNegotiations_ShouldReturnAllNegotiations()
        {
            // Arrange
            var product = SeedProduct(_faker.Commerce.ProductName(), _faker.Finance.Amount(1000, 10000));
            var customer = SeedCustomer(Guid.NewGuid(), _faker.Name.FullName());
            var negotiation = SeedNegotiation(product.Id, product.Price.Value, _faker.Finance.Amount(1, 999), customer.Id.Value);

            // Act
            var result = await AsAdmin<INegotiationsApi>().GetNegotiationsAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(1, result.Content.Count());
        }

        //[Fact]
        //public async Task GetNegotiation_ShouldReturnSpecifiedNegotiation()
        //{


        //    // Check if each test data item is present in the returned products
        //    Assert.Contains(returnedNegotiation, testData);
        //    Assert.Contains(negotiation, testData);
        //}

        //[Fact]
        //public async Task GetNegotiation_ShouldThrowNotFoundException()
        //{

        //    // Act and Assert
        //    await Assert.ThrowsAsync<NotFoundException>(async () =>
        //    {
        //        await negotiationService.GetNegotiationAsync(nonExistingNegotiationId);
        //    });
        //}

        //[Theory]
        //[InlineData("123abc", 1.78/*, "user2"*/)]
        //[InlineData("123ac", 1.99/*, "user3"*/)]
        //public async Task CreateNegotiationAsync_ShouldCreateNegotiation_WhenValidData(Guid productId, decimal proposedPrice/*, string userId*/)
        //{
        //    // Arrange

        //    // Create a ClaimsPrincipal with the desired user
        //    //ClaimsIdentity claimsIdentity = new ClaimsIdentity(new Claim[]
        //    //{
        //    //	new Claim(ClaimTypes.Name, userId),
        //    //});

        //    //ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        //    //// Inject the claimsPrincipal into IExecutionContext
        //    //var claimsProviderMock = new Mock<IExecutionContext>();
        //    //claimsProviderMock.Setup(cp => cp.UserClaimsPrincipal).Returns(claimsPrincipal);

        //    // ...

        //    // Assert
        //    Assert.NotNull(createdNegotiation);
        //    Assert.Equal(negotiationInputModel.ProductId, createdNegotiation.ProductId);
        //    Assert.Equal(negotiationInputModel.ProposedPrice, createdNegotiation.ProposedPrice);
        //}

        //[Theory]
        //[InlineData("123ab", 20, "")]
        //[InlineData("", 20, "user2")]
        //public async Task CreateNegotiationAsync_ShouldNotCreateNegotiation_WhenInvalidData(string productId, decimal proposedPrice, string userId)
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

        //[Theory]
        ////[InlineData(true, NegotiationStatus.Closed, 0)]
        //[InlineData(false, NegotiationStatus.Open, 1)]
        //public async Task RespondToNegotiationProposalAsync_ShouldUpdateNegotiation(bool isApproved, NegotiationStatus expectedStatus, int expectedRetries)
        //{
        //    // Arrange
        //    var negotiationService = _fixture.NegotiationService;
        //    _fixture.PopulateData();

        //    var existingNegotiation = (await negotiationService.GetNegotiationsAsync()).First();

        //    // Act
        //    var result = await negotiationService.RespondToNegotiationProposalAsync(existingNegotiation.NegotiationId, isApproved);

        //    // Assert
        //    Assert.Equal(UpdateResultType.Success, result);
        //    Assert.Equal(expectedStatus, existingNegotiation.Status);
        //    //Assert.Equal(expectedRetries, existingNegotiation.RetriesLeft);
        //}

        //[Fact]
        //public async Task DeleteNegotiationAsync_NonExistingNegotiation_ShouldNotRemoveProduct()
        //{

        //    // Assert
        //    Assert.False(result);
        //}
    }
}
