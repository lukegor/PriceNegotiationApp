using NSubstitute;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Negotiations
{
    public class NegotiationBehaviorShould
    {
        private readonly IIdGenerator _idGenerator;
        private readonly TimeProvider _timeProvider;
        private readonly NegotiationFactory _factory;

        public NegotiationBehaviorShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _timeProvider = Substitute.For<TimeProvider>();
            _factory = new NegotiationFactory(_idGenerator, _timeProvider);
        }

        [Fact]
        public void TryNegotiate_DecreaseRetriesAndUpdateTimestamp()
        {
            // Arrange
            var initialTime = new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero);
            _timeProvider.GetUtcNow().Returns(initialTime);
            var negotiation = CreateNegotiation(startingRetries: 3);

            var expectedUpdatedAt = new DateTimeOffset(2026, 04, 01, 09, 00, 00, TimeSpan.Zero);

            // Act
            negotiation.TryNegotiate(200m, new ProposedPrice(120m), 100m, expectedUpdatedAt);

            // Assert
            Assert.Equal(1, negotiation.RetriesLeft);
            Assert.Equal(expectedUpdatedAt.UtcDateTime, negotiation.UpdatedAt);
        }

        [Fact]
        public void TryNegotiate_ThrowDomainException_WhenNoRetriesLeft()
        {
            // Arrange
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));
            var negotiation = CreateNegotiation(startingRetries: 1);

            // Act
            var exception = Record.Exception(() =>
                negotiation.TryNegotiate(200m, new ProposedPrice(110m), 100m, new DateTimeOffset(2026, 04, 01, 10, 00, 00, TimeSpan.Zero)));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("No retries left for this negotiation.", exception.Message);
        }

        [Fact]
        public void Close_SetStatusToClosedAndReject()
        {
            // Arrange
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));
            var negotiation = CreateNegotiation(startingRetries: 3);

            // Act
            negotiation.Close();

            // Assert
            Assert.Equal(NegotiationStatus.Closed, negotiation.Status);
            Assert.False(negotiation.IsAccepted);
        }

        [Fact]
        public void Close_ThrowDomainException_WhenAlreadyClosed()
        {
            // Arrange
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));
            var negotiation = CreateNegotiation(startingRetries: 3);
            negotiation.Close();

            // Act
            var exception = Record.Exception(negotiation.Close);

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("Negotiation must be open.", exception.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Archive_CloseNegotiationAndSetAcceptedState(bool isApproved)
        {
            // Arrange
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));
            var negotiation = CreateNegotiation(startingRetries: 3);

            // Act
            negotiation.Archive(isApproved, new DateTimeOffset(2026, 04, 01, 09, 00, 00, TimeSpan.Zero));

            // Assert
            Assert.Equal(NegotiationStatus.Closed, negotiation.Status);
            Assert.Equal(isApproved, negotiation.IsAccepted);
        }

        [Fact]
        public void ResetRetries_SetProvidedRetriesAndTimestamp()
        {
            // Arrange
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));
            var negotiation = CreateNegotiation(startingRetries: 3);
            negotiation.TryNegotiate(200m, new ProposedPrice(120m), 100m, new DateTimeOffset(2026, 04, 01, 09, 00, 00, TimeSpan.Zero));

            var resetTime = new DateTimeOffset(2026, 04, 01, 10, 30, 00, TimeSpan.Zero);

            // Act
            negotiation.ResetRetries(5, resetTime);

            // Assert
            Assert.Equal(5, negotiation.RetriesLeft);
            Assert.Equal(resetTime.UtcDateTime, negotiation.UpdatedAt);
        }

        [Fact]
        public void ResetRetries_ThrowDomainException_WhenClosed()
        {
            // Arrange
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));
            var negotiation = CreateNegotiation(startingRetries: 3);
            negotiation.Close();

            // Act
            var exception = Record.Exception(() =>
                negotiation.ResetRetries(3, new DateTimeOffset(2026, 04, 01, 10, 30, 00, TimeSpan.Zero)));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("Negotiation must be open.", exception.Message);
        }

        private Negotiation CreateNegotiation(int startingRetries)
        {
            _idGenerator.NewId().Returns(Guid.Parse("10000000-0000-0000-0000-000000000001"));

            return _factory.Create(
                ProductId.From(Guid.Parse("20000000-0000-0000-0000-000000000001")),
                new ProductPrice(100m).Value,
                new ProposedPrice(120m),
                CustomerId.From(Guid.Parse("30000000-0000-0000-0000-000000000001")),
                startingRetries,
                200m);
        }
    }
}
