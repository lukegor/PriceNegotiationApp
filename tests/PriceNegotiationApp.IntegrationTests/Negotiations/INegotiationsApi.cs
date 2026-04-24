using PriceNegotiationApp.Contracts.Negotiations.Dto.Requests;
using PriceNegotiationApp.Contracts.Negotiations.Dto.Response;
using Refit;

namespace PriceNegotiationApp.IntegrationTests.Negotiations
{
    public interface INegotiationsApi
    {
        [Get("/api/v1/negotiations/all")]
        Task<IApiResponse<IEnumerable<NegotiationResponseDto>>> GetNegotiationsAsync([AliasAs("$filter")] string? filter = null);

        [Get("/api/v1/negotiations/{id}")]
        Task<IApiResponse<NegotiationResponseDto>> GetNegotiationByIdAsync(Guid id);

        [Post("/api/v1/negotiations")]
        Task<IApiResponse<NegotiationResponseDto>> CreateNegotiationAsync([Body] CreateNegotiationRequestDto negotiation);

        [Put("/api/v1/negotiations/{id}")]
        Task<IApiResponse<NegotiationResponseDto>> UpdateNegotiationAsync(Guid id, [Body] UpdateNegotiationRequestDto negotiation);

        [Delete("/api/v1/negotiations/{id}")]
        Task<IApiResponse> DeleteNegotiationAsync(Guid id);
    }
}
