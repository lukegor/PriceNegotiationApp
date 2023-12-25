using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Models.DTO;
using PriceNegotiationApp.Models;
using PriceNegotiationApp.Utility;
using static PriceNegotiationApp.Controllers.AuthenticationController;
using System.IdentityModel.Tokens.Jwt;
using PriceNegotiationApp.Extensions.Conversions;
using PriceNegotiationApp.Auth.Authentication.JWT;

namespace PriceNegotiationApp.Services
{
    public class AuthService
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

		public async Task<AuthResponseDTO> AuthenticateAsync(LoginModel model)
		{
			var user = await _userManager.FindByNameAsync(model.Username);

			if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
			{
				_logger.LogWarning("Authentication failed for username: {Username}", model.Username);
				return new AuthResponseDTO { ErrorMessage = "Invalid Authentication" };
			}

			var signingCredentials = _jwtHandler.GetSigningCredentials();
			var claims = await _jwtHandler.GetClaims(user);

			var tokenOptions = _jwtHandler.GenerateTokenOptions(signingCredentials, claims);

			var token = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

			_logger.LogInformation("User {Username} authenticated successfully.", model.Username);

			return new AuthResponseDTO { IsAuthSuccessful = true, Token = token };
		}

		public async Task SignOutAsync()
		{
			await _signInManager.SignOutAsync();
			_logger.LogInformation("User signed out.");
		}

		public async Task<IdentityResult> RegisterUserAsync(RegisterUserDTO userForRegistration)
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
	}
}
