namespace PriceNegotiationApp.Application.Common;

public static class ErrorCodes
{
    public const string ProductNotFound = "product_not_found";
    public const string NegotiationNotFound = "negotiation_not_found";
    public const string NegotiationClosed = "negotiation_closed";
    public const string NegotiationAlreadyOpen = "negotiation_already_open";
    public const string NoProposalsRemaining = "no_proposals_remaining";
    public const string ProposalExceedsLimit = "proposal_exceeds_limit";
    public const string EmailAlreadyRegistered = "email_already_registered";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountLocked = "account_locked";
    public const string Forbidden = "forbidden";
    public const string ConcurrencyConflict = "conflict";
    public const string InternalError = "internal_error";
}
