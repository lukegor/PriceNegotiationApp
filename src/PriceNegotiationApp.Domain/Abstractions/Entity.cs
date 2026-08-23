using PriceNegotiationApp.Domain.Exceptions;

namespace PriceNegotiationApp.Domain.Abstractions;

public abstract class Entity
{
    protected static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new DomainException(rule.Message);
        }
    }
}
