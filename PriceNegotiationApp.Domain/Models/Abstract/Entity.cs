namespace PriceNegotiationApp.Domain.Models.Abstract
{
    public abstract class Entity<TId>
    {
        public TId? Id { get; protected init; }

        protected static void CheckRule(IBusinessRule rule)
        {
            if (rule.IsBroken())
            {
                throw new DomainException(rule.Message);
            }
        }
    }
}
