using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Withdraw
{
    internal static void MapWithdraw(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                WithdrawHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, principal.ToCallerContext(), ct);
            return TypedResults.NoContent();
        })
        .RequireAuthorization();
    }
}
