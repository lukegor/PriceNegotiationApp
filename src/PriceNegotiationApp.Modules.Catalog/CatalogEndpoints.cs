using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Modules.Catalog.Features.Products;

namespace PriceNegotiationApp.Modules.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products")
            .WithTags("Products")
            .RequireAuthorization();
        group.MapList();
        group.MapGetOne();
        group.MapCreate();
        group.MapUpdate();
        group.MapDelete();
        return app;
    }
}
