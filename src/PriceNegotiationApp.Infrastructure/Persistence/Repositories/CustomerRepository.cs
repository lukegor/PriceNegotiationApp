using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(AppDbContext db, IUnitOfWork uow) : ICustomerRepository
{
    public async Task<CustomerId> GetOrCreateAsync(Guid identityUserId, CancellationToken ct)
    {
        var existing = await GetByIdentityAsync(identityUserId, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = Customer.Create(identityUserId);
        await db.Customers.AddAsync(customer, ct);
        await uow.SaveChangesAsync(ct);
        return customer.Id;
    }

    public Task<Customer?> GetByIdentityAsync(Guid identityUserId, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId, ct);
}
