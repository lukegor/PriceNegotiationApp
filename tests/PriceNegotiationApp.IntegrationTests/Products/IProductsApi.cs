using PriceNegotiationApp.Contracts.Products.Dtos;
using PriceNegotiationApp.Contracts.Products.Dtos.Responses;
using Refit;

namespace PriceNegotiationApp.IntegrationTests.Products
{
    public interface IProductsApi
    {
        [Get("/api/v1/products/all")]
        Task<IApiResponse<IEnumerable<ProductDto>>> GetProductsAsync([Query(), AliasAs("$filter")] string filter = null);

        [Get("/api/v1/products/{id}")]
        Task<IApiResponse<ProductResponseDto>> GetProductByIdAsync([AliasAs("id")] Guid id);
    }
}
