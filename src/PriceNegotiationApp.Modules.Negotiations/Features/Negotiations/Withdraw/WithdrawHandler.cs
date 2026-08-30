using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;
using PriceNegotiationApp.Modules.Negotiations.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Withdraw;

internal sealed class WithdrawHandler(NegotiationsDbContext db, TimeProvider clock)
{
    public async Task HandleAsync(Guid id, CallerContext caller, CancellationToken ct)
    {
        var negotiation = await NegotiationAccess.RequireAsync(db, id, ct);

        if (caller.IsInRole(UserRoles.Admin))
        {
            db.Negotiations.Remove(negotiation);
        }
        else
        {
            if (!await NegotiationAccess.IsOwnerAsync(db, caller.UserId, negotiation, ct))
            {
                throw new ForbiddenAccessException();
            }

            negotiation.Withdraw(clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
    }
}
