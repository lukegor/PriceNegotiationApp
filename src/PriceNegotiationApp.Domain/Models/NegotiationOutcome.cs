namespace PriceNegotiationApp.Domain.Models;

public enum NegotiationOutcome
{
    CounterProposed = 1,
    AutoRejected = 2,
    NoProposalsRemaining = 3,
}
