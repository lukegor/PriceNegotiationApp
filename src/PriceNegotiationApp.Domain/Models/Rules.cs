using PriceNegotiationApp.Domain.Abstractions;
using PriceNegotiationApp.Domain.ValueObjects;

namespace PriceNegotiationApp.Domain.Models;

internal sealed record ProductNameMustNotBeEmpty(string? Value) : IBusinessRule
{
    public bool IsBroken() => string.IsNullOrWhiteSpace(Value);

    public string Message => "Product name must not be empty.";
}

internal sealed record NegotiationMustBeOpenRule(NegotiationStatus Status) : IBusinessRule
{
    public bool IsBroken() => Status != NegotiationStatus.Open;

    public string Message => "Negotiation is already closed.";
}
