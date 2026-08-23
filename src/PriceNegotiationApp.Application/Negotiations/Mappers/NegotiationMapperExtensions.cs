using PriceNegotiationApp.Application.Negotiations.Dtos;
using PriceNegotiationApp.Domain.Models.Negotiations;

namespace PriceNegotiationApp.Application.Negotiations.Mappers
{
    public static class NegotiationMapperExtensions
    {
        extension(Negotiation negotiation)
        {
            public NegotiationResultDto ToResultDto()
            {
                return new NegotiationResultDto(
                    negotiation.Id.Value,
                    negotiation.ProductId.Value,
                    negotiation.ProposedPrice.Value,
                    negotiation.IsAccepted == true,
                    negotiation.RetriesLeft,
                    negotiation.Status,
                    negotiation.UserId.Value);
            }
        }
    }
}
