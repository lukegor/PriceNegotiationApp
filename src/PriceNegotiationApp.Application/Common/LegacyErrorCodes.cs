namespace PriceNegotiationApp.Application.Common;

/// <summary>Temporary home for feature-specific error codes until their modules extract them (Tasks 4-6).</summary>
public static class LegacyErrorCodes
{
    public const string EmailAlreadyRegistered = "email_already_registered";

    public const string InvalidCredentials = "invalid_credentials";

    public const string AccountLocked = "account_locked";

    public const string RegistrationInvalid = "registration_invalid";

    public const string NegotiationClosed = "negotiation_closed";

    public const string ProposalExceedsLimit = "proposal_exceeds_limit";

    public const string ProductNotFound = "product_not_found";

    public const string NegotiationNotFound = "negotiation_not_found";

    public const string NegotiationAlreadyOpen = "negotiation_already_open";

    public const string NoProposalsRemaining = "no_proposals_remaining";
}


