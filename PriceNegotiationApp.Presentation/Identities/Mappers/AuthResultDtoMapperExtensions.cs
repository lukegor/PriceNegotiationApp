using PriceNegotiationApp.Application.Common.Identities.Dtos;
using PriceNegotiationApp.Contracts.Identities.Dtos.Responses;

namespace PriceNegotiationApp.Presentation.Identities.Mappers
{
    public static class AuthResultDtoMapperExtensions
    {
        extension(AuthResultDto authResultDto)
        {
            public AuthResponseDto ToResponseDto()
            {
                return new AuthResponseDto(
                    authResultDto.IsAuthSuccessful,
                    authResultDto.ErrorMessage,
                    authResultDto.Token);
            }
        }
    }
}
