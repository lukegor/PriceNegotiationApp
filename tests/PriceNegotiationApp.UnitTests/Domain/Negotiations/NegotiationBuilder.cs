using Bogus;
using PriceNegotiationApp.Domain.Models.Customers;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.UnitTests.Domain.Negotiations
{
    public class NegotiationBuilder
    {
        private readonly Faker _faker = new("pl");

        private NegotiationId _id;
        private ProductId _productId;
        private ProposedPrice _proposedPrice = new ProposedPrice(0.9m);
        private CustomerId _customerId;
        private DateTimeOffset _createdAt;
        private decimal _maxAllowedPrice = 100000000;
        private int _remainingRetries = 3;

        public NegotiationBuilder()
        {
            _id = NegotiationId.From(_faker.Random.Guid());
            _productId = ProductId.From(_faker.Random.Guid());
            _customerId = CustomerId.From(_faker.Random.Guid());
            _createdAt = _faker.Date.RecentOffset();
        }

        public Negotiation Build()
        {
            return new Negotiation(
                _id,
                _productId,
                _proposedPrice,
                _customerId,
                _createdAt,
                _remainingRetries,
                _maxAllowedPrice
            );
        }

        public NegotiationBuilder WithId(Guid id)
        {
            _id = NegotiationId.From(id);
            return this;
        }

        public NegotiationBuilder WithProductId(Guid productId)
        {
            _productId = ProductId.From(productId);
            return this;
        }

        public NegotiationBuilder WithProposedPrice(decimal price)
        {
            _proposedPrice = new ProposedPrice(price);
            return this;
        }

        public NegotiationBuilder WithCustomerId(Guid customerId)
        {
            _customerId = CustomerId.From(customerId);
            return this;
        }

        public NegotiationBuilder WithCreatedAt(DateTimeOffset createdAt)
        {
            _createdAt = createdAt;
            return this;
        }

        public NegotiationBuilder WithRetries(int retries)
        {
            _remainingRetries = retries;
            return this;
        }

        public NegotiationBuilder WithMaxAllowedPrice(decimal maxAllowedPrice)
        {
            _maxAllowedPrice = maxAllowedPrice;
            return this;
        }

        public static Negotiation Default() => new NegotiationBuilder().Build();
    }
}
