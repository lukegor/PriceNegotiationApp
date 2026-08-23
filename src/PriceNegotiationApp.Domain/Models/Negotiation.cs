using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.Exceptions;
using PriceNegotiationApp.Domain.Policy;
using PriceNegotiationApp.Domain.ValueObjects;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Domain.Models;

public sealed class Negotiation : Entity
{
    /// <summary>Base price snapshot taken at creation; protects ongoing negotiations from later product price changes.</summary>
    public decimal BasePrice { get; private set; }

    public decimal CurrentOffer { get; private set; }

    public NegotiationId Id { get; private set; }

    public ProductId ProductId { get; private set; }

    public CustomerId CustomerId { get; private set; }

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
        NegotiationId id, ProductId productId, CustomerId customerId, decimal basePrice, decimal currentOffer,
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

    public static Negotiation Start(CustomerId customerId, Product product, decimal initialOffer, DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureWithinLimit(product.Price, initialOffer, policy);
        return new Negotiation(NegotiationId.From(Guid.CreateVersion7()), product.Id, customerId, product.Price, initialOffer, now);
    }

    public NegotiationOutcome CounterPropose(decimal offer, DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureOpen();
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

    /// <summary>
    /// Staff rejects the current offer. The negotiation deliberately stays open so the
    /// customer may spend a remaining proposal; it terminates only via Accept,
    /// auto-rejection, or withdrawal.
    /// </summary>
    public void Decline() => EnsureOpen();

    public int RemainingProposals(INegotiationPolicy policy) =>
        Math.Max(0, policy.MaxProposalsPerNegotiation - ProposalsUsed);

    private void Decide(NegotiationStatus terminalStatus, DateTimeOffset now)
    {
        EnsureOpen();
        Status = terminalStatus;
        DecidedAtUtc = now;
    }

    private void EnsureOpen()
    {
        if (Status != NegotiationStatus.Open)
        {
            throw new ClosedNegotiationException();
        }
    }

    private static void EnsureWithinLimit(decimal basePrice, decimal offer, INegotiationPolicy policy)
    {
        var limit = decimal.Round(basePrice * policy.ProposalMultiplierLimit, 2);
        ValueObjects.Price.From(offer);
        if (offer > limit)
        {
            throw new ProposalExceedsLimitException(limit);
        }
    }
}
