using PriceNegotiationApp.Domain;
using PriceNegotiationApp.Domain.Models.Customer;
using PriceNegotiationApp.Domain.Models.Negotiations;
using PriceNegotiationApp.Domain.Models.Negotiations.ValueObjects;
using PriceNegotiationApp.Domain.Models.Products;

namespace PriceNegotiationApp.Application.Negotiations
{
    /// <summary>
    /// <see cref="Negotiation"/> domain service
    /// </summary>
    public interface INegotiationDomainService
    {
        Negotiation CreateNegotiation(ProductId productId, decimal productPrice, ProposedPrice proposedPrice,
            CustomerId userId);
        void ResetRetries(Negotiation negotiation);
        void TryNegotiate(Negotiation negotiation, ProposedPrice proposedPrice, decimal productPrice);
    }

    /// <inheritdoc cref="INegotiationDomainService"/>
    public class NegotiationDomainService(NegotiationFactory negotiationFactory,
        TimeProvider timeProvider) : INegotiationDomainService
    {
        private const int MaxPriceMultiplier = 2;
        private const int StartingRetries = 3;

        public Negotiation CreateNegotiation(ProductId productId, decimal productPrice, ProposedPrice proposedPrice,
            CustomerId userId)
        {
            var maxAllowedPrice = CalculateMaxAllowedPrice(MaxPriceMultiplier, productPrice);

            if (proposedPrice.Value > maxAllowedPrice)
            {
                throw new DomainException(
                    $"Proposed price cannot exceed {maxAllowedPrice:C}.");
            }

            return negotiationFactory.Create(productId, productPrice, proposedPrice, userId, StartingRetries, MaxPriceMultiplier);
        }

        public void TryNegotiate(Negotiation negotiation, ProposedPrice proposedPrice, decimal productPrice)
        {
            var maxAllowedPrice = CalculateMaxAllowedPrice(MaxPriceMultiplier, productPrice);
            if (proposedPrice.Value > maxAllowedPrice)
            {
                throw new DomainException(
                    $"Proposed price cannot exceed {maxAllowedPrice:C}.");
            }

            negotiation.TryNegotiate(MaxPriceMultiplier, proposedPrice, productPrice, timeProvider.GetUtcNow());
        }

        public void ResetRetries(Negotiation negotiation)
        {
            negotiation.ResetRetries(StartingRetries, timeProvider.GetUtcNow());
        }

        private static decimal CalculateMaxAllowedPrice(int multiplier, decimal productPrice)
        {
            return multiplier * productPrice;
        }
    }
}
