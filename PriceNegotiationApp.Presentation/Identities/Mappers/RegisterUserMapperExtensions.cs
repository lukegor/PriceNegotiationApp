using PriceNegotiationApp.Application.Common.Identities.Requests.Commands;
using PriceNegotiationApp.Contracts.Identities.Dtos.Requests;

namespace PriceNegotiationApp.Presentation.Identities.Mappers
{
    public static class RegisterUserMapperExtensions
    {
        extension(RegisterUserRequestDto request)
        {
            public RegisterUserCommand ToCommand()
            {
                return new RegisterUserCommand(
                    request.UserName,
                    request.Name,
                    request.Email,
                    request.StreetAddress,
                    request.City,
                    request.State,
                    request.PostalCode,
                    request.Password,
                    request.ConfirmPassword);
            }
        }
    }
}
