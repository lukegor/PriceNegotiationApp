using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Infrastructure.ListMine;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.ListMine;

internal static class ListMineEndpoint
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
