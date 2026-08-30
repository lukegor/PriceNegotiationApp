using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.Api.Endpoints.Catalog.Create;
using PriceNegotiationApp.Api.Endpoints.Catalog.Delete;
using PriceNegotiationApp.Api.Endpoints.Catalog.Get;
using PriceNegotiationApp.Api.Endpoints.Catalog.List;
using PriceNegotiationApp.Api.Endpoints.Catalog.Update;

namespace PriceNegotiationApp.Api.Endpoints.Catalog;

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
