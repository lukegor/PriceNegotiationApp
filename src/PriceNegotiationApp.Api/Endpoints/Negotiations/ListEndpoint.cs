using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.List;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.List;

internal static class ListEndpoint
{
    internal static void MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ListNegotiationsHandler handler, CancellationToken ct,
                int page = 1, int pageSize = 20) =>
            TypedResults.Ok(await handler.HandleAsync(new PageQuery(page, pageSize), ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
