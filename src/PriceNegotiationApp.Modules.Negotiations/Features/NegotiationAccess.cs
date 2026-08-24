using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Negotiations.Domain;
using PriceNegotiationApp.Modules.Negotiations.Persistence;

namespace PriceNegotiationApp.Modules.Negotiations.Features;

internal static class NegotiationAccess
{
    public static async Task<Negotiation> RequireAsync(NegotiationsDbContext db, Guid id, CancellationToken ct) =>
        await db.Negotiations.FirstOrDefaultAsync(n => n.Id == NegotiationId.From(id), ct)
        ?? throw new NotFoundException(nameof(Negotiation), id);

    public static async Task<Negotiation> RequireOwnedAsync(
        NegotiationsDbContext db, CallerContext caller, Guid id, CancellationToken ct)
    {
        var negotiation = await RequireAsync(db, id, ct);
        if (!await IsOwnerAsync(db, caller.UserId, negotiation, ct))
        {
            throw new ForbiddenAccessException();
        }

        return negotiation;
    }

    public static async Task<bool> CanAccessAsync(
        NegotiationsDbContext db, CallerContext caller, Negotiation negotiation, CancellationToken ct)
    {
        if (caller.IsInRole(UserRoles.Admin) || caller.IsInRole(UserRoles.Staff))
        {
            return true;
        }

        return await IsOwnerAsync(db, caller.UserId, negotiation, ct);
    }

    public static async Task<bool> IsOwnerAsync(
        NegotiationsDbContext db, Guid identityUserId, Negotiation negotiation, CancellationToken ct)
    {
        var customer = await CustomerByIdentityAsync(db, identityUserId, ct);
        return customer is not null && customer.Id == negotiation.CustomerId;
    }

    public static Task<Customer?> CustomerByIdentityAsync(
        NegotiationsDbContext db, Guid identityUserId, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId, ct);

    public static async Task<CustomerId> GetOrCreateCustomerIdAsync(
        NegotiationsDbContext db, Guid identityUserId, CancellationToken ct)
    {
        var existing = await CustomerByIdentityAsync(db, identityUserId, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = Customer.Create(identityUserId);
        await db.Customers.AddAsync(customer, ct);
        return customer.Id;
    }

    public static async Task<Negotiation?> FindOpenAsync(
        NegotiationsDbContext db, Guid productId, Guid identityUserId, CancellationToken ct)
    {
        var customer = await CustomerByIdentityAsync(db, identityUserId, ct);
        return customer is null
            ? null
            : await db.Negotiations.AsNoTracking().FirstOrDefaultAsync(
                n => n.ProductId == productId && n.CustomerId == customer.Id && n.Status == NegotiationStatus.Open,
                ct);
    }
}



