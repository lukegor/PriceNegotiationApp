namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class NegotiationErrorCodes
{
    public const string NegotiationClosed = "negotiation_closed";

    public const string ProposalExceedsLimit = "proposal_exceeds_limit";

    public const string NegotiationAlreadyOpen = "negotiation_already_open";

    public const string NoProposalsRemaining = "no_proposals_remaining";
}
