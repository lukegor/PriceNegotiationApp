namespace PriceNegotiationApp.Domain.Models.Customer
{
    public class Customer
    {
        /// <summary>
        /// Customer Id
        /// </summary>
        public CustomerId Id { get; private set; }

        /// <summary>
        /// Id of associated user in Identity system
        /// </summary>
        public Guid IdentityId { get; private set; }

        public string Name { get; private set; }

        private Customer() { }

        internal Customer(CustomerId customerId, Guid identityId, string Name)
        {
            Id = customerId;
            IdentityId = identityId;
            Name = Name;
        }
    }
}
