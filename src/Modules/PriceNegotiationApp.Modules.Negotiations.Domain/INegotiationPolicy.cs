namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal interface INegotiationPolicy
{
    int MaxProposalsPerNegotiation { get; }

    decimal ProposalMultiplierLimit { get; }
}


