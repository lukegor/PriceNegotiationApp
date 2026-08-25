namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal sealed class Negotiation
{
    /// <summary>Base price snapshot taken at creation; protects ongoing negotiations from later product price changes.</summary>
    public decimal BasePrice { get; private set; }

    public decimal CurrentOffer { get; private set; }

    public NegotiationId Id { get; private set; }

    public Guid ProductId { get; private set; }

    public CustomerId CustomerId { get; private set; }

    public NegotiationStatus Status { get; private set; }

    /// <summary>Total proposals recorded, including the initial one.</summary>
    public int ProposalsUsed { get; private set; }

    /// <summary>Proposal budget snapshotted from the active policy at creation time.</summary>
    public int MaxProposals { get; private set; }

    /// <summary>Offer multiplier limit snapshotted from the active policy at creation time.</summary>
    public decimal OfferMultiplierLimit { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastProposalAtUtc { get; private set; }

    /// <summary>Most recent staff reject-current-offer action; does not change status.</summary>
    public DateTimeOffset? LastStaffActionAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public uint Version { get; private set; }

    private Negotiation()
    {
    }

    private Negotiation(
        NegotiationId id, Guid productId, CustomerId customerId, decimal basePrice, decimal initialOffer,
        INegotiationPolicy policy, DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        CustomerId = customerId;
        BasePrice = basePrice;
        CurrentOffer = initialOffer;
        MaxProposals = policy.MaxProposalsPerNegotiation;
        OfferMultiplierLimit = policy.ProposalMultiplierLimit;
        Status = NegotiationStatus.Open;
        ProposalsUsed = 1;
        CreatedAtUtc = createdAtUtc;
        LastProposalAtUtc = createdAtUtc;
    }

    public static Negotiation Start(
        CustomerId customerId, Guid productId, decimal basePriceSnapshot, decimal initialOffer,
        DateTimeOffset now, INegotiationPolicy policy)
    {
        EnsureWithinLimit(basePriceSnapshot, initialOffer, policy.ProposalMultiplierLimit);
        return new Negotiation(NegotiationId.From(Guid.CreateVersion7()), productId, customerId,
            basePriceSnapshot, initialOffer, policy, now);
    }

    public NegotiationOutcome CounterPropose(decimal offer, DateTimeOffset now)
    {
        EnsureOpen();
        if (ProposalsUsed >= MaxProposals)
        {
            return NegotiationOutcome.NoProposalsRemaining;
        }

        try
        {
            EnsureWithinLimit(BasePrice, offer, OfferMultiplierLimit);
        }
        catch (ProposalExceedsLimitException)
        {
            Status = NegotiationStatus.Rejected;
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
    /// customer may spend a remaining proposal; the proposal budget is untouched.
    /// It terminates only via Accept, auto-rejection, or withdrawal.
    /// </summary>
    public void RejectCurrentOffer(DateTimeOffset now)
    {
        EnsureOpen();
        LastStaffActionAtUtc = now;
    }

    /// <summary>Owner abandons the negotiation; state becomes terminal, history is preserved.</summary>
    public void Withdraw(DateTimeOffset now) => Decide(NegotiationStatus.Withdrawn, now);

    public int RemainingProposals() => Math.Max(0, MaxProposals - ProposalsUsed);

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

    private static void EnsureWithinLimit(decimal basePrice, decimal offer, decimal multiplierLimit)
    {
        var limit = decimal.Round(basePrice * multiplierLimit, 2);
        Price.From(offer);
        if (offer > limit)
        {
            throw new ProposalExceedsLimitException(limit);
        }
    }
}
