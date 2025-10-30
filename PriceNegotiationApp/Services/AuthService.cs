using Microsoft.AspNetCore.Identity;
using static PriceNegotiationApp.Controllers.AuthenticationController;
using System.IdentityModel.Tokens.Jwt;
using PriceNegotiationApp.Auth.Authentication.JWT;
using PriceNegotiationApp.Domain.Models.Dto.Requests;
using PriceNegotiationApp.Domain.Models.Dto;
using PriceNegotiationApp.Utility.Utility;
using PriceNegotiationApp.Domain.Models.Users;
using PriceNegotiationApp.Domain.Models.Mappers;

namespace PriceNegotiationApp.Services
{
	public interface IAuthService
	{
		Task<AuthResponseDTO> AuthenticateAsync(LoginRequestDto dto);
		Task SignOutAsync();
		Task<IdentityResult> RegisterUserAsync(RegisterUserRequestDto userForRegistration);
		Task ValidateEmailUniqueness(string email);
		Task ValidateUserNameUniqueness(string username);
    }

    public class AuthService : IAuthService
    {
		private readonly SignInManager<IdentityUser> _signInManager;
		private readonly UserManager<IdentityUser> _userManager;
		private readonly JwtManager _jwtHandler;
		private readonly ILogger<AuthService> _logger;

		public AuthService(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, JwtManager jwtHandler, ILogger<AuthService> logger)
		{
			_signInManager = signInManager;
			_userManager = userManager;
			_jwtHandler = jwtHandler;
			_logger = logger;
		}

		public async Task<AuthResponseDTO> AuthenticateAsync(LoginRequestDto dto)
		{
			var user = await _userManager.FindByNameAsync(dto.Username);

			if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
			{
				_logger.LogWarning("Authentication failed for username: {Username}", dto.Username);
				return new AuthResponseDTO { ErrorMessage = "Invalid Authentication" };
			}

			var token = await _jwtHandler.GenerateToken(user);

            _logger.LogInformation("User {Username} authenticated successfully.", dto.Username);

			return new AuthResponseDTO { IsAuthSuccessful = true, Token = token };
		}

		public async Task SignOutAsync()
		{
			await _signInManager.SignOutAsync();
			_logger.LogInformation("User signed out.");
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

        public async Task ValidateEmailUniqueness(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

			if (user != null)
			{
				throw new InvalidOperationException("Email is not unique.");
            }
        }

        public async Task ValidateUserNameUniqueness(string username)
        {
            var user = await _userManager.FindByNameAsync(username);

            if (user != null)
            {
                throw new InvalidOperationException("UserName is not unique.");
            }
        }
    }
}
