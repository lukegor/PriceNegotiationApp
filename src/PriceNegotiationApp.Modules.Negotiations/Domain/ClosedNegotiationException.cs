using PriceNegotiationApp.SharedKernel;
namespace PriceNegotiationApp.Modules.Negotiations.Domain;

/// <summary>Thrown when an operation targets a negotiation that has already reached a terminal state.</summary>
internal sealed class ClosedNegotiationException()
    : DomainException("Negotiation is already closed.");

