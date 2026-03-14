using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    public class NegotiationFactory(IIdGenerator _idGenerator, TimeProvider timeProvider)
    {
        public Negotiation Create(ProductId productId, decimal productPrice, ProposedPrice proposedPrice, CustomerId customerId,
            int startingRetries, double maxPriceAllowed)
        {
            var id = _idGenerator.NewId();
            return new Negotiation(NegotiationId.From(id), productId, productPrice, proposedPrice, customerId,
                timeProvider.GetUtcNow(), startingRetries, maxPriceAllowed);
        }
    }
}
