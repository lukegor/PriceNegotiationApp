using PriceNegotiationApp.Modules.Negotiations.Application;

namespace PriceNegotiationApp.Modules.Negotiations.Application.CounterPropose;

internal sealed record CounterProposalResponse(string Outcome, NegotiationResponse Negotiation);
