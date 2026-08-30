using PriceNegotiationApp.Modules.Negotiations.Domain;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

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

internal static class NegotiationResponses
{
    internal static NegotiationResponse ToResponse(Negotiation n) =>
        new(n.Id.Value, n.ProductId, n.BasePrice.Value, n.CurrentOffer.Value, n.Status.ToString(),
            n.ProposalsUsed, n.RemainingProposals(), n.CreatedAtUtc, n.LastProposalAtUtc, n.DecidedAtUtc);
}

internal sealed record StaffActionResponse(string Outcome, NegotiationResponse Negotiation);
