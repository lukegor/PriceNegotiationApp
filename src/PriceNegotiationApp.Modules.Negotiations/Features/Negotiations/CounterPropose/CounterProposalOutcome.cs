using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.CounterPropose;

internal sealed record CounterProposalOutcome(string Outcome, NegotiationResponse Negotiation);
