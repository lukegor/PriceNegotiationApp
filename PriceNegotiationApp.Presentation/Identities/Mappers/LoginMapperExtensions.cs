using PriceNegotiationApp.Application.Common.Identities.Requests.Commands;
using PriceNegotiationApp.Contracts.Identities.Dtos.Requests;

namespace PriceNegotiationApp.Presentation.Identities.Mappers
{
    public static class LoginMapperExtensions
    {
        extension(LoginRequestDto request)
        {
            public LoginCommand ToCommand()
            {
                return new LoginCommand(
                    request.Username,
                    request.Password);
            }
        }
    }
}
