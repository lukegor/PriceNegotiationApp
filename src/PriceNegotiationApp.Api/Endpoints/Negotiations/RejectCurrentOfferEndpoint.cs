using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.RejectCurrentOffer;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.RejectCurrentOffer;

internal static class RejectCurrentOfferEndpoint
{
    internal static void MapRejectCurrentOffer(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/decline", async (Guid id, RejectCurrentOfferHandler handler,
                CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
