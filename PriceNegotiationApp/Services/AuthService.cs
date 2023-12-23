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

		public AuthService(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, JwtManager jwtHandler)
		{
			_signInManager = signInManager;
			_userManager = userManager;
			_jwtHandler = jwtHandler;
		}

		public async Task<AuthResponseDTO> AuthenticateAsync(LoginModel model)
		{
			var user = await _userManager.FindByNameAsync(model.Username);

			if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
				return new AuthResponseDTO { ErrorMessage = "Invalid Authentication" };

			var signingCredentials = _jwtHandler.GetSigningCredentials();
			var claims = await _jwtHandler.GetClaims(user);

			var tokenOptions = _jwtHandler.GenerateTokenOptions(signingCredentials, claims);

			var token = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

			return new AuthResponseDTO { IsAuthSuccessful = true, Token = token };
		}

		public async Task SignOutAsync()
		{
			await _signInManager.SignOutAsync();
		}

		public async Task<IdentityResult> RegisterUserAsync(RegisterUserDTO userForRegistration)
		{
			ApplicationUser user = userForRegistration.ToDb();
			var result = await _userManager.CreateAsync(user, userForRegistration.Password);

			if (result.Succeeded)
			{
				await _userManager.AddToRoleAsync(user, Roles.Role_Customer);
			}

			return result;
		}
	}
}
