using PriceNegotiationApp.BuildingBlocks;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

/// <summary>Transitional: saves every registered context that has pending changes.
/// Removed together with the legacy projects once modules own their save points.</summary>
public sealed class UnitOfWork(IEnumerable<DbContext> contexts) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        var saved = 0;
        foreach (var db in contexts)
        {
            if (!db.ChangeTracker.HasChanges())
            {
                continue;
            }

            try
            {
                saved += await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(ErrorCodes.ConcurrencyConflict,
                    "The resource was modified concurrently. Reload and retry.");
            }
        }

        return saved;
    }
}


