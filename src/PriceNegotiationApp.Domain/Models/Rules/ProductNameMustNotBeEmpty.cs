using PriceNegotiationApp.Domain.Abstractions;

namespace PriceNegotiationApp.Domain.Models;

internal sealed record ProductNameMustNotBeEmpty(string? Value) : IBusinessRule
{
    public bool IsBroken() => string.IsNullOrWhiteSpace(Value);

    public string Message => "Product name must not be empty.";
}
