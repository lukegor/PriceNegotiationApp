using PriceNegotiationApp.Application.Negotiations.Dtos;
using PriceNegotiationApp.Contracts.Negotiations.Dto.Response;

namespace PriceNegotiationApp.Presentation.Negotiations.Mappers
{
    public static class NegotiationResultDtoMapperExtensions
    {
        extension(NegotiationResultDto negotiationResultDto)
        {
            public NegotiationResponseDto ToResponseDto()
            {
                return new NegotiationResponseDto(
                    negotiationResultDto.NegotiationId,
                    negotiationResultDto.ProductId,
                    negotiationResultDto.ProposedPrice,
                    negotiationResultDto.IsAccepted,
                    negotiationResultDto.RetriesLeft,
                    negotiationResultDto.Status.ToString(),
                    negotiationResultDto.UserId);
            }
        }
    }
}
