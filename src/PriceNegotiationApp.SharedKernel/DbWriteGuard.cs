using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PriceNegotiationApp.SharedKernel;

/// <summary>
/// Translates PostgreSQL uniqueness violations raised during SaveChanges into
/// caller-supplied semantic exceptions, so check-then-insert races surface as
/// conflicts instead of HTTP 500.
/// </summary>
public static class DbWriteGuard
{
    public static bool IsUniqueViolation(Exception exception, out string constraintName)
    {
        constraintName = string.Empty;
        for (var current = (Exception?)exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
            {
                constraintName = postgres.ConstraintName ?? string.Empty;
                return true;
            }
        }

        return false;
    }

    public static async Task SaveOrConflictAsync(
        this DbContext db, Func<string, Exception> conflictFactory, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraint))
        {
            throw conflictFactory(constraint);
        }
    }
}
