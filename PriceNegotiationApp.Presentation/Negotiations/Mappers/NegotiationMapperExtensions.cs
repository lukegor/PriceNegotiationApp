using PriceNegotiationApp.Contracts.Negotiations.Dto.Response;
using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Presentation.Negotiations.Mappers
{
    public static class NegotiationMapperExtensions
    {
        extension(Negotiation negotiation)
        {
            public NegotiationResponseDto ToResponseDto()
            {
                return new NegotiationResponseDto(
                    negotiation.Id.Value,
                    negotiation.ProductId.Value,
                    negotiation.ProposedPrice.Value,
                    negotiation.IsAccepted != true ? false : true,
                    negotiation.RetriesLeft,
                    negotiation.Status.Value,
                    negotiation.UserId.Value);
            }
        }
    }
}
