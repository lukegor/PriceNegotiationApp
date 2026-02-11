using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.Login;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.RegisterUser;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Responses;

namespace PriceNegotiationApp.Application.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> AuthenticateAsync(LoginRequestDto dto);
        Task<IdentityResult> RegisterUserAsync(RegisterUserRequestDto userForRegistration);
    }
}
