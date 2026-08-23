using PriceNegotiationApp.Domain.Abstractions;

namespace PriceNegotiationApp.Domain.Models;

internal sealed record ProductNameMustNotBeTooLong(string Value) : IBusinessRule
{
    public const int MaxLength = 200;

    public bool IsBroken() => Value.Trim().Length > MaxLength;

    public string Message => $"Product name must not exceed {MaxLength} characters.";
}
