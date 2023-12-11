using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PriceNegotiationApp.Auth
{
	public class JwtManager
	{
		private readonly JwtSettings _jwtSettings;
		private readonly UserManager<IdentityUser> _userManager;

		public JwtManager(IOptions<JwtSettings> jwtSettings, UserManager<IdentityUser> userManager)
		{
			_jwtSettings = jwtSettings.Value;
			_userManager = userManager;
		}

		// WARNING: Ensure that _jwtSettings.SecurityKey is a string of at least 32 characters (32 bytes) for HmacSha256
		// Required by new JwtSecurityTokenHandler().WriteToken(tokenOptions); since new version (Nuget package JsonWebToken version 8.0 and transitive ...IdentityModel. token-related packages post 7.0)
		public SigningCredentials GetSigningCredentials()
		{

			var key = Encoding.UTF8.GetBytes(_jwtSettings.SecurityKey);
			var secret = new SymmetricSecurityKey(key);

			return new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);
		}

		public async Task<List<Claim>> GetClaims(IdentityUser user)
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.Name, user.Email),
				new Claim(ClaimTypes.NameIdentifier, user.Id)
			};

			var roles = await _userManager.GetRolesAsync(user);
			foreach (var role in roles)
			{
				claims.Add(new Claim(ClaimTypes.Role, role));
			}

			return claims;
		}

		public JwtSecurityToken GenerateTokenOptions(SigningCredentials signingCredentials, List<Claim> claims)
		{
			var tokenOptions = new JwtSecurityToken(
				issuer: _jwtSettings.ValidIssuer,
				audience: _jwtSettings.ValidAudience,
				claims: claims,
				expires: DateTime.Now.AddMinutes(Convert.ToDouble(_jwtSettings.ExpiryInMinutes)),
				signingCredentials: signingCredentials);

			return tokenOptions;
		}
	}
}
