using Microsoft.AspNetCore.Mvc;
using PriceNegotiationApp.Api.Contracts;
using PriceNegotiationApp.Api.Extensions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Features.Products;

namespace PriceNegotiationApp.Api.Modules;

public static class ProductsModule
{
    public static IEndpointRouteBuilder MapProductsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products").WithTags("Products");

        group.MapGet("/",
                async ([AsParameters] ProductListRequest query, IProductService products, CancellationToken ct) =>
                    TypedResults.Ok(await products.ListAsync(query.ToQuery(), ct)))
            .CacheOutput(WebApplicationBuilderExtensions.ShortCachePolicy)
            .AllowAnonymous();

        group.MapGet("/{id:guid}",
                async (Guid id, IProductService products, CancellationToken ct) =>
                    TypedResults.Ok(await products.GetAsync(id, ct)))
            .WithName("GetProductById")
            .CacheOutput(WebApplicationBuilderExtensions.ShortCachePolicy)
            .AllowAnonymous();

        group.MapPost("/", async (CreateProductRequest request, IProductService products, CancellationToken ct) =>
        {
            var created = await products.CreateAsync(request.Name, request.Price, ct);
            return TypedResults.CreatedAtRoute(created, "GetProductById", new { id = created.Id });
        }).RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, IProductService products, CancellationToken ct) =>
                TypedResults.Ok(await products.UpdateAsync(id, request.Name, request.Price, ct)))
            .RequireRoles(UserRoles.Admin, UserRoles.Staff);

        group.MapDelete("/{id:guid}", async (Guid id, IProductService products, CancellationToken ct) =>
        {
            await products.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        }).RequireRoles(UserRoles.Admin);

        return app;
    }
}
