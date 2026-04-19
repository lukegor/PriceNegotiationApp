using NSubstitute;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;
using PriceNegotiationApp.Domain.Models.Products.ValueObjects;

namespace PriceNegotiationApp.UnitTests.Domain.Negotiations
{
    public class NegotiationDomainServiceShould
    {
        private readonly IIdGenerator _idGenerator;
        private readonly TimeProvider _timeProvider;
        private readonly NegotiationFactory _factory;
        private readonly NegotiationDomainService _service;

        public NegotiationDomainServiceShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _timeProvider = Substitute.For<TimeProvider>();
            _factory = new NegotiationFactory(_idGenerator, _timeProvider);
            _service = new NegotiationDomainService(_factory, _timeProvider);
        }

        [Fact]
        public void CreateNegotiation_CreateOpenNegotiation_WhenProposalIsWithinLimit()
        {
            // Arrange
            _idGenerator.NewId().Returns(Guid.Parse("50000000-0000-0000-0000-000000000001"));
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));

            // Act
            var negotiation = _service.CreateNegotiation(
                ProductId.From(Guid.Parse("50000000-0000-0000-0000-000000000002")),
                100m,
                new ProposedPrice(150m),
                CustomerId.From(Guid.Parse("50000000-0000-0000-0000-000000000003")));

            // Assert
            Assert.Equal(NegotiationStatus.Open, negotiation.Status);
            Assert.Equal(2, negotiation.RetriesLeft);
        }

        [Fact]
        public void CreateNegotiation_ThrowDomainException_WhenProposalExceedsLimit()
        {
            // Arrange
            _idGenerator.NewId().Returns(Guid.Parse("50000000-0000-0000-0000-000000000011"));
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));

            // Act
            var exception = Record.Exception(() => _service.CreateNegotiation(
                ProductId.From(Guid.Parse("50000000-0000-0000-0000-000000000012")),
                100m,
                new ProposedPrice(201m),
                CustomerId.From(Guid.Parse("50000000-0000-0000-0000-000000000013"))));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("Proposed price cannot exceed ¤200.00.", exception.Message);
        }

        [Fact]
        public void TryNegotiate_DecreaseRetries_WhenProposalIsWithinLimit()
        {
            // Arrange
            _idGenerator.NewId().Returns(Guid.Parse("50000000-0000-0000-0000-000000000021"));
            _timeProvider.GetUtcNow().Returns(
                new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 04, 01, 09, 00, 00, TimeSpan.Zero));

            var negotiation = _factory.Create(
                ProductId.From(Guid.Parse("50000000-0000-0000-0000-000000000022")),
                100m,
                new ProposedPrice(150m),
                CustomerId.From(Guid.Parse("50000000-0000-0000-0000-000000000023")),
                3,
                200m);

            // Act
            _service.TryNegotiate(negotiation, new ProposedPrice(180m), 100m);

            // Assert
            Assert.Equal(1, negotiation.RetriesLeft);
        }

        [Fact]
        public void TryNegotiate_ThrowDomainException_WhenProposalExceedsLimit()
        {
            // Arrange
            _idGenerator.NewId().Returns(Guid.Parse("50000000-0000-0000-0000-000000000031"));
            _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero));

            var negotiation = _factory.Create(
                ProductId.From(Guid.Parse("50000000-0000-0000-0000-000000000032")),
                100m,
                new ProposedPrice(150m),
                CustomerId.From(Guid.Parse("50000000-0000-0000-0000-000000000033")),
                3,
                200m);

            // Act
            var exception = Record.Exception(() => _service.TryNegotiate(negotiation, new ProposedPrice(220m), 100m));

            // Assert
            Assert.IsType<DomainException>(exception);
            Assert.Equal("Proposed price cannot exceed ¤200.00.", exception.Message);
        }

        [Fact]
        public void ResetRetries_SetsRetryCountToConfiguredStartValue()
        {
            // Arrange
            _idGenerator.NewId().Returns(Guid.Parse("50000000-0000-0000-0000-000000000041"));
            _timeProvider.GetUtcNow().Returns(
                new DateTimeOffset(2026, 04, 01, 08, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 04, 01, 09, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 04, 01, 10, 00, 00, TimeSpan.Zero));

            var negotiation = _factory.Create(
                ProductId.From(Guid.Parse("50000000-0000-0000-0000-000000000042")),
                100m,
                new ProposedPrice(150m),
                CustomerId.From(Guid.Parse("50000000-0000-0000-0000-000000000043")),
                3,
                200m);

            _service.TryNegotiate(negotiation, new ProposedPrice(160m), 100m);

            // Act
            _service.ResetRetries(negotiation);

            // Assert
            Assert.Equal(3, negotiation.RetriesLeft);
        }
    }
}
