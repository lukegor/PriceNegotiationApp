using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Modules.Negotiations.Features.Negotiations;

internal static class Create
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
                CreateNegotiationHandler handler, CancellationToken ct) =>
            TypedResults.Created("/api/v1/negotiations/mine",
                await handler.HandleAsync(request, principal.ToCallerContext(), ct)))
        .RequireRoles(UserRoles.Customer);
    }
}
