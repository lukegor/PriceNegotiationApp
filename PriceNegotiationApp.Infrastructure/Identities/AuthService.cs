using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PriceNegotiationApp.Application;
using PriceNegotiationApp.Application.Common.Identities;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.Login;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.RegisterUser;
using PriceNegotiationApp.Application.Common.Identities.Dtos.Responses;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Application.Services;
using PriceNegotiationApp.Infrastructure.Identities.Mappers;
using System.Security.Claims;

namespace PriceNegotiationApp.Infrastructure.Identities
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIdentityService _identityService;
        private readonly IJwtTokenGenerator _jwtHandler;
        private readonly ILogger<AuthService> _logger;

        public AuthService(UserManager<ApplicationUser> userManager, IIdentityService identityService, IJwtTokenGenerator jwtHandler, ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _identityService = identityService;
            _jwtHandler = jwtHandler;
            _logger = logger;
        }

        public async Task<AuthResponseDto> AuthenticateAsync(LoginRequestDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);

            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                _logger.LogWarning("Authentication failed for username: {Username}", dto.Username);
                return new AuthResponseDto { ErrorMessage = "Invalid Authentication" };
            }

            var claims = await GetClaims(user);
            var token = await _jwtHandler.GenerateToken(claims);

            _logger.LogInformation("User {Username} authenticated successfully.", dto.Username);

            return new AuthResponseDto { IsAuthSuccessful = true, Token = token };
        }

        public async Task<IdentityResult> RegisterUserAsync(RegisterUserRequestDto userForRegistration)
        {
            ApplicationUser user = userForRegistration.ToDb();
            var result = await _userManager.CreateAsync(user, userForRegistration.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, Roles.Role_Customer);
                _logger.LogInformation("User {UserName} registered successfully.", user.UserName);
            }

            return result;
        }

        private async Task<IReadOnlyCollection<Claim>> GetClaims(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return claims;
        }
    }
}
