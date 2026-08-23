using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Responses;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Application.Abstractions;

public interface IProductRepository
{
    Task<PagedResult<ProductResponse>> SearchAsync(ProductQuery query, CancellationToken ct);

    Task<Product?> GetAsync(ProductId id, CancellationToken ct);

    IQueryable<Product> Query();

    Task AddAsync(Product product, CancellationToken ct);

    void Remove(Product product);
}
