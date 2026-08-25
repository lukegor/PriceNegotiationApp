using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class ListMine
{
    internal static void MapListMine(this RouteGroupBuilder group)
    {
        group.MapGet("/mine", async (ClaimsPrincipal principal, ListMyNegotiationsHandler handler,
                CancellationToken ct, int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(
                new PageQuery(page, pageSize), principal.ToCallerContext(), ct)))
        .RequireRoles(UserRoles.Customer);
    }
}
