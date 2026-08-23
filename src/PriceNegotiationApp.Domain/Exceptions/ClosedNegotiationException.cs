namespace PriceNegotiationApp.Domain.Exceptions;

/// <summary>Thrown when an operation targets a negotiation that has already reached a terminal state.</summary>
public sealed class ClosedNegotiationException()
    : DomainException("Negotiation is already closed.");
