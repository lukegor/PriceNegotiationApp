using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;

namespace PriceNegotiationApp.Domain.Models.Negotiations
{
    /// <summary>
    /// <see cref="Negotiation"/> domain service
    /// </summary>
    public interface INegotiationDomainService
    {
        void ResetRetries(Negotiation negotiation);
        void TryNegotiate(Negotiation negotiation, ProposedPrice proposedPrice);
    }

    /// <inheritdoc cref="INegotiationDomainService"/>
    public class NegotiationDomainService(
        TimeProvider timeProvider,
        INegotiationPolicy negotiationPolicy) : INegotiationDomainService
    {
        public void TryNegotiate(Negotiation negotiation, ProposedPrice proposedPrice)
        {
            negotiation.TryNegotiate(proposedPrice, timeProvider.GetUtcNow());
        }

        public void ResetRetries(Negotiation negotiation)
        {
            var retries = negotiationPolicy.CalculateRetries(negotiation.UserId, negotiation.ProductId);
            negotiation.ResetRetries(retries, timeProvider.GetUtcNow());
        }
    }
}
