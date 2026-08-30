using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Modules.Negotiations.Features.Negotiations.Create;
using PriceNegotiationApp.SharedKernel;
using System.Security.Claims;

namespace PriceNegotiationApp.Api.Endpoints.Negotiations.Create;

internal static class CreateEndpoint
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateNegotiationRequest request, ClaimsPrincipal principal,
                CreateNegotiationHandler handler, CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, principal.ToCallerContext(), ct);
                return TypedResults.CreatedAtRoute(response, "GetNegotiationById", new { id = response.Id });
            })
        .AddEndpointFilter<ValidateRequestFilter<CreateNegotiationRequest>>()
        .RequireRoles(UserRoles.Customer);
    }
}
