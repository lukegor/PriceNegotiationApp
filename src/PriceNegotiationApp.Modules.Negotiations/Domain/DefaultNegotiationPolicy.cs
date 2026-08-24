namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal sealed class DefaultNegotiationPolicy : INegotiationPolicy
{
    public int MaxProposalsPerNegotiation => 3;

    public decimal ProposalMultiplierLimit => 2.0m;
}


