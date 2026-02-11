using PriceNegotiationApp.Application.Negotiations.Dto.Response;
using PriceNegotiationApp.Domain.Models.Negotiations;
using System;
using System.Collections.Generic;
using System.Text;

namespace PriceNegotiationApp.Application.Negotiations.Mappers
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
                    negotiation.Status,
                    negotiation.UserId);
            }
        }
    }
}
