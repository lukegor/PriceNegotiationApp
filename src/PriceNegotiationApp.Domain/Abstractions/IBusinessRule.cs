namespace PriceNegotiationApp.Domain.Abstractions;

public interface IBusinessRule
{
    bool IsBroken();

    string Message { get; }
}
