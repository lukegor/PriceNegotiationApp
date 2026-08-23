using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Responses;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Application.Features.Products;

public sealed class ProductService(IProductRepository products, IUnitOfWork uow) : IProductService
{
    public Task<PagedResult<ProductResponse>> ListAsync(ProductQuery query, CancellationToken ct) =>
        products.SearchAsync(query, ct);

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(id), ct)
                      ?? throw new NotFoundException(nameof(Product), id);
        return new ProductResponse(product.Id.Value, product.Name, product.Price);
    }

    public async Task<ProductResponse> CreateAsync(string name, decimal price, CancellationToken ct)
    {
        var product = Product.Create(name, price);
        await products.AddAsync(product, ct);
        await uow.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, string name, decimal price, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(id), ct)
                      ?? throw new NotFoundException(nameof(Product), id);
        product.Update(name, price);
        await uow.SaveChangesAsync(ct);
        return new ProductResponse(product.Id.Value, product.Name, product.Price);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var product = await products.GetAsync(ProductId.From(id), ct)
                      ?? throw new NotFoundException(nameof(Product), id);
        products.Remove(product);
        await uow.SaveChangesAsync(ct);
    }
}
