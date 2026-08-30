using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.Withdraw;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Withdraw;

internal static class WithdrawEndpoint
{
    internal static void MapWithdraw(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal,
                WithdrawHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, principal.ToCallerContext(), ct);
            return TypedResults.NoContent();
        });
    }
}
