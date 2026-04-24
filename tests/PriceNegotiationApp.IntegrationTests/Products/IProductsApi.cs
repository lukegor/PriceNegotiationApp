using PriceNegotiationApp.Contracts.Products.Dtos;
using PriceNegotiationApp.Contracts.Products.Dtos.Requests;
using PriceNegotiationApp.Contracts.Products.Dtos.Responses;
using Refit;

namespace PriceNegotiationApp.IntegrationTests.Products
{
    public interface IProductsApi
    {
        [Get("/api/v1/products/all")]
        Task<IApiResponse<IEnumerable<ProductDto>>> GetProductsAsync([AliasAs("$filter")] string? filter = null);

        [Get("/api/v1/products/{id}")]
        Task<IApiResponse<ProductResponseDto>> GetProductByIdAsync([AliasAs("id")] Guid id);

        [Post("/api/v1/products")]
        Task<IApiResponse<ProductResponseDto>> CreateProductAsync([Body] ProductRequestDto request);

        [Put("/api/v1/products/{id}")]
        Task<IApiResponse<ProductResponseDto>> UpdateProductAsync([AliasAs("id")] Guid id, [Body] ProductRequestDto request);

        [Delete("/api/v1/products/{id}")]
        Task<IApiResponse> DeleteProductAsync([AliasAs("id")] Guid id);
    }
}
