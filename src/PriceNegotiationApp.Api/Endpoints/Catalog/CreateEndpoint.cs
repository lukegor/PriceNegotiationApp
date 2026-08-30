using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api;
using PriceNegotiationApp.Modules.Catalog.Features.Products.Create;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Api.Endpoints.Catalog.Create;

internal static class CreateEndpoint
{
    internal static void MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (CreateProductRequest request, CreateProductHandler handler,
                CancellationToken ct) =>
            {
                var response = await handler.HandleAsync(request, ct);
                return TypedResults.CreatedAtRoute(response, "GetProductById", new { id = response.Id });
            })
        .AddEndpointFilter<ValidateRequestFilter<CreateProductRequest>>()
        .RequireRoles(UserRoles.Admin, UserRoles.Staff);
    }
}
