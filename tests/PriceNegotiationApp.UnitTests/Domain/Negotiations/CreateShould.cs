using NSubstitute;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Negotiations
{
    public class CreateShould
    {
        private readonly NegotiationFactory _negotiationFactory;
        private readonly IIdGenerator _idGenerator;

        public CreateShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _negotiationFactory = new NegotiationFactory(_idGenerator);
        }

        [Fact]
        public void CreateNegotiation()
        {
            // Arrange
            var productId = ProductId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var initialPrice = new ProductPrice(100m);

            var customerId = CustomerId.From(Guid.Parse("11111111-1111-1111-1111-111111111112"));

            var expectedNegotiationId = Guid.Parse("11111111-1111-1111-1111-111111111113");
            _idGenerator.NewId().Returns(expectedNegotiationId);

            var proposedPrice = new ProposedPrice(200);

            // Act
            var negotiation = _negotiationFactory.Create(productId, initialPrice.Value, proposedPrice, customerId);

            // Assert
            Assert.NotNull(negotiation);
            Assert.Equal(expectedNegotiationId, negotiation.Id.Value);
            Assert.Equal(productId, negotiation.ProductId);
            Assert.Equal(proposedPrice, negotiation.ProposedPrice);
            Assert.Equal(NegotiationStatus.Open, negotiation.Status);
        }
    }
}
