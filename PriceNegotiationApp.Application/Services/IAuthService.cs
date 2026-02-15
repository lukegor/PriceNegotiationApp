using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Application.Common.Identities.Dtos;
using PriceNegotiationApp.Application.Common.Identities.Requests.Commands;

namespace PriceNegotiationApp.Application.Services
{
    public interface IAuthService
    {
        Task<AuthResultDto> AuthenticateAsync(LoginCommand command);
        Task<IdentityResult> RegisterUserAsync(RegisterUserCommand command);
    }
}
