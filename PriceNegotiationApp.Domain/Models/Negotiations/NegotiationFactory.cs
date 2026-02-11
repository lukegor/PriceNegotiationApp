using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    public class NegotiationFactory
    {
        private readonly IIdGenerator _idGenerator;

        public NegotiationFactory(IIdGenerator idGenerator)
        {
            _idGenerator = idGenerator;
        }

        public Negotiation Create(ProductId productId, decimal productPrice, ProposedPrice proposedPrice, Guid customerId)
        {
            var id = _idGenerator.NewId();
            return new Negotiation(NegotiationId.From(id), productId, productPrice, proposedPrice, customerId);
        }
    }
}
