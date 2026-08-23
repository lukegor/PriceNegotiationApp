using PriceNegotiationApp.Domain.Models.Customers;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    public class NegotiationFactory(IIdGenerator idGenerator, TimeProvider timeProvider, INegotiationPolicy negotiationPolicy)
    {
        public Negotiation Create(ProductId productId, decimal productPrice, ProposedPrice proposedPrice, CustomerId customerId)
        {
            var id = idGenerator.NewId();

            var retries = negotiationPolicy.CalculateRetries(customerId, productId);
            var maxAllowedPrice = negotiationPolicy.CalculateMaxAllowedPrice(productPrice, productId);

            var now = timeProvider.GetUtcNow();

            var negotiation = new Negotiation(NegotiationId.From(id), productId, proposedPrice, customerId,
                now, retries, maxAllowedPrice);

            negotiation.TryNegotiate(proposedPrice, now);

            return negotiation;
        }
    }
}
