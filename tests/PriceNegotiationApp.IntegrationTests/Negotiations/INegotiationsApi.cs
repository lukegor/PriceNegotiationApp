using PriceNegotiationApp.Contracts.Negotiations.Dto.Response;
using Refit;

namespace PriceNegotiationApp.IntegrationTests.Negotiations
{
    public interface INegotiationsApi
    {
        [Get("/api/v1/negotiations")]
        Task<IApiResponse<IEnumerable<NegotiationResponseDto>>> GetNegotiationsAsync([AliasAs("$filter")] string filter = null);
    }
}
