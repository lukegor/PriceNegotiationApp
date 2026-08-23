namespace PriceNegotiationApp.Domain.Models.Abstract
{
    public interface IBusinessRule
    {
        string Message { get; }

        bool IsBroken();

        static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new DomainException(message);
            }
        }
    }
}
