using PriceNegotiationApp.Domain;

namespace PriceNegotiationApp.Infrastructure
{
    public class SystemIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
