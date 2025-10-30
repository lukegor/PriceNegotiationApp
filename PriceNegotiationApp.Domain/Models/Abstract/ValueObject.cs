namespace PriceNegotiationApp.Domain.Models.Abstract
{
    public abstract class ValueObject
    {
        protected static void CheckRule(IBusinessRule rule)
        {
            if (rule.IsBroken())
            {
                throw new Exception(rule.Message);
            }
        }
    }
}
