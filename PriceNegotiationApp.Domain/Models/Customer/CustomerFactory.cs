namespace PriceNegotiationApp.Domain.Models.Customer
{
    public class CustomerFactory
    {
        private readonly IIdGenerator _idGenerator;

        public CustomerFactory(IIdGenerator idGenerator)
        {
            _idGenerator = idGenerator;
        }

        public Customer Create(Guid identityId, string name)
        {
            return new Customer(CustomerId.From(_idGenerator.NewId()), identityId, name);
        }
    }
}
