namespace PriceNegotiationApp.Domain.Policy;

public interface INegotiationPolicy
{
    int MaxProposalsPerNegotiation { get; }

    decimal ProposalMultiplierLimit { get; }
}
