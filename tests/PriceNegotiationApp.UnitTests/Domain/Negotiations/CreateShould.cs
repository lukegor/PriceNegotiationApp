using Bogus;
using FluentAssertions;
using NSubstitute;
using PriceNegotiationApp.Application.Negotiations;
using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.UnitTests.Domain.Negotiations
{
    public class CreateShould
    {
        private readonly NegotiationFactory _negotiationFactory;
        private readonly IIdGenerator _idGenerator;
        private readonly TimeProvider _timeProvider;
        private readonly INegotiationPolicy _negotiationPolicy;
        private readonly Faker _faker = new("pl");

        public CreateShould()
        {
            _idGenerator = Substitute.For<IIdGenerator>();
            _timeProvider = Substitute.For<TimeProvider>();
            _negotiationPolicy = new DefaultNegotiationPolicy();
            _negotiationFactory = new NegotiationFactory(_idGenerator, _timeProvider, _negotiationPolicy);
        }

        [Fact]
        public void CreateNegotiation()
        {
            // Arrange
            var initialPrice = _faker.Finance.Amount(1001, 100000000);
            var maxPrice = initialPrice * 2m;

            var expectedNegotiation = new NegotiationBuilder()
                .WithMaxAllowedPrice(maxPrice)
                .WithRetries(2)
                .Build();

            _idGenerator.NewId().Returns(expectedNegotiation.Id.Value);

            // Act
            var negotiation = _negotiationFactory.Create(expectedNegotiation.ProductId, initialPrice,
                expectedNegotiation.ProposedPrice, expectedNegotiation.UserId);

            // Assert
            negotiation.Should().BeEquivalentTo(expectedNegotiation, options => options
                .Excluding(n => n.CreatedAt)
                .Excluding(n => n.UpdatedAt));
        }
    }
}
