namespace PriceNegotiationApp.IntegrationTests.Negotiations
{
    public class NegotiationServiceTest : IClassFixture<IntegrationTestFactory>
    {
        //private readonly ITestOutputHelper _output;
        //private readonly NegotiationServiceTestFixture _fixture;

        //public NegotiationServiceTest(ITestOutputHelper output, NegotiationServiceTestFixture fixture)
        //{
        //    _output = output;
        //    _fixture = fixture;
        //}

        //[Fact]
        //public async Task GetNegotiations_ShouldReturnAllNegotiations()
        //{

        //    // Ensure that the number of returned products matches the number of test data items
        //    Assert.Equal(testData.Count(), resultList.Count);

        //    // Check if each test data item is present in the returned products
        //    foreach (var product in returnedModels)
        //    {
        //        Assert.Contains(product, testData);
        //    }
        //}

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
