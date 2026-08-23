namespace PriceNegotiationApp.Application.Responses;

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
