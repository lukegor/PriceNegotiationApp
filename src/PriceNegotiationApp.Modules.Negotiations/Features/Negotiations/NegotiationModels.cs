using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal sealed class CreateNegotiationRequest
{
    public Guid ProductId { get; init; }

    public decimal ProposedPrice { get; init; }
}

internal sealed class CounterProposalRequest
{
    public decimal ProposedPrice { get; init; }
}

internal sealed record NegotiationResponse(
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

internal sealed record CounterProposalOutcome(string Outcome, NegotiationResponse Negotiation);

/// <summary>Machine-readable error codes owned by this feature (frozen contract).</summary>
internal static class NegotiationErrorCodes
{
    public const string NegotiationClosed = "negotiation_closed";

    public const string ProposalExceedsLimit = "proposal_exceeds_limit";

    public const string NegotiationAlreadyOpen = "negotiation_already_open";

    public const string NoProposalsRemaining = "no_proposals_remaining";
}

internal static class NegotiationResponses
{
    internal static NegotiationResponse ToResponse(Negotiation n) =>
        new(n.Id.Value, n.ProductId, n.BasePrice.Value, n.CurrentOffer.Value, n.Status.ToString(),
            n.ProposalsUsed, n.RemainingProposals(), n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
}

internal sealed record StaffActionResponse(string Outcome, NegotiationResponse Negotiation);
