using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

public sealed class CreateNegotiationRequest
{
    public Guid ProductId { get; init; }

    public decimal ProposedPrice { get; init; }
}

public sealed class CounterProposalRequest
{
    public decimal ProposedPrice { get; init; }
}

public sealed record NegotiationResponse(
    Guid Id,
    Guid ProductId,
    decimal BasePrice,
    decimal CurrentOffer,
    string Status,
    int ProposalsUsed,
    int ProposalsRemaining,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastProposalAtUtc,
    DateTimeOffset? DecidedAtUtc);

public sealed record CounterProposalOutcome(string Outcome, NegotiationResponse Negotiation);

/// <summary>Machine-readable error codes owned by this feature (frozen contract).</summary>
public static class NegotiationErrorCodes
{
    public const string NegotiationClosed = "negotiation_closed";

    public const string ProposalExceedsLimit = "proposal_exceeds_limit";

    public const string NegotiationAlreadyOpen = "negotiation_already_open";

    public const string NoProposalsRemaining = "no_proposals_remaining";
}

internal static class NegotiationResponses
{
    internal static NegotiationResponse ToResponse(Negotiation n, INegotiationPolicy policy) =>
        new(n.Id.Value, n.ProductId, n.BasePrice, n.CurrentOffer, n.Status.ToString(),
            n.ProposalsUsed, n.RemainingProposals(policy), n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
}
