using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Negotiation : Entity
{
    public NegotiationId Id { get; private set; }

    public ProductId ProductId { get; private set; }

    public CustomerId CustomerId { get; private set; }

    /// <summary>Base price snapshot taken at creation; protects ongoing negotiations from later product price changes.</summary>
    public Price BasePrice { get; private set; }

    public Price CurrentOffer { get; private set; }

    public NegotiationStatus Status { get; private set; }

    /// <summary>Total proposals recorded, including the initial one.</summary>
    public int ProposalsUsed { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastProposalAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public uint Version { get; private set; }

    private Negotiation()
    {
    }

    private Negotiation(
        NegotiationId id, ProductId productId, CustomerId customerId, Price basePrice, Price currentOffer,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        CustomerId = customerId;
        BasePrice = basePrice;
        CurrentOffer = currentOffer;
        Status = NegotiationStatus.Open;
        ProposalsUsed = 1;
        CreatedAtUtc = createdAtUtc;
        LastProposalAtUtc = createdAtUtc;
    }

    public static Negotiation Start(CustomerId customerId, Product product, Price initialOffer, DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureWithinLimit(product.Price, initialOffer, policy);
        return new Negotiation(NegotiationId.From(Guid.CreateVersion7()), product.Id, customerId, product.Price, initialOffer, now);
    }

    public NegotiationOutcome CounterPropose(Price offer, DateTimeOffset now, INegotiationPolicy policy)
    {
        CheckRule(new NegotiationMustBeOpenRule(Status));
        if (ProposalsUsed >= policy.MaxProposalsPerNegotiation)
        {
            return NegotiationOutcome.NoProposalsRemaining;
        }

        try
        {
            EnsureWithinLimit(BasePrice, offer, policy);
        }
        catch (ProposalExceedsLimitException)
        {
            Status = NegotiationStatus.Declined;
            DecidedAtUtc = now;
            return NegotiationOutcome.AutoRejected;
        }

        CurrentOffer = offer;
        ProposalsUsed++;
        LastProposalAtUtc = now;
        return NegotiationOutcome.CounterProposed;
    }

    public void Accept(DateTimeOffset now) => Decide(NegotiationStatus.Accepted, now);

    public void Decline(DateTimeOffset now) => Decide(NegotiationStatus.Declined, now);

    public int RemainingProposals(INegotiationPolicy policy) =>
        Math.Max(0, policy.MaxProposalsPerNegotiation - ProposalsUsed);

    private void Decide(NegotiationStatus terminalStatus, DateTimeOffset now)
    {
        CheckRule(new NegotiationMustBeOpenRule(Status));
        Status = terminalStatus;
        DecidedAtUtc = now;
    }

    private static void EnsureWithinLimit(Price basePrice, Price offer, INegotiationPolicy policy)
    {
        var limit = basePrice.Value * policy.ProposalMultiplierLimit;
        if (offer.Value > limit)
        {
            throw new ProposalExceedsLimitException(limit);
        }
    }
}
