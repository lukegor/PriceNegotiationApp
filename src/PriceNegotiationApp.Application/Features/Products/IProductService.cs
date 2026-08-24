using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Application.Responses;

namespace PriceNegotiationApp.Application.Features.Products;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> ListAsync(ProductQuery query, CancellationToken ct);

    Task<ProductResponse> GetAsync(Guid id, CancellationToken ct);

    Task<ProductResponse> CreateAsync(string name, decimal price, CancellationToken ct);

    Task<ProductResponse> UpdateAsync(Guid id, string name, decimal price, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}

