using PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.RegisterUser;

namespace PriceNegotiationApp.Infrastructure.Identities.Mappers
{
    public static class RegisterUserDtoExtensionsMapper
    {
        extension(RegisterUserRequestDto registerUser)
        {
            public ApplicationUser ToDb()
            {
                return new ApplicationUser(registerUser.Name, registerUser.StreetAddress, registerUser.City, registerUser.State,
                    registerUser.PostalCode)
                {
                    UserName = registerUser.UserName,
                    Email = registerUser.Email,
                };
            }
        }
    }
}