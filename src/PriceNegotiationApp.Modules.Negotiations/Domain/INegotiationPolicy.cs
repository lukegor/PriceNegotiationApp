namespace PriceNegotiationApp.Modules.Negotiations.Domain;

public interface INegotiationPolicy
{
    int MaxProposalsPerNegotiation { get; }

    decimal ProposalMultiplierLimit { get; }
}


