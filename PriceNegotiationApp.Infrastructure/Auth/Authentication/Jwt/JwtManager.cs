using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PriceNegotiationApp.Application.Security;
using PriceNegotiationApp.Infrastructure.Identities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PriceNegotiationApp.Infrastructure.Auth.Authentication.Jwt
{
    public class JwtManager : IJwtTokenGenerator
    {
        private readonly JwtSettings _jwtSettings;
        private readonly UserManager<ApplicationUser> _userManager;

        public JwtManager(IOptions<JwtSettings> jwtSettings, UserManager<ApplicationUser> userManager)
        {
            _jwtSettings = jwtSettings.Value;
            _userManager = userManager;
        }

        /// <summary>
        /// Generates a JSON Web Token (JWT) containing the specified claims.
        /// </summary>
        /// <param name="claims">Collection of claims to include in the generated token as key-value pairs, like "email": "test@test.pl".</param>
        /// <returns>The generated JWT containing the provided claims as a string.</returns>
        public async Task<string> GenerateToken(IReadOnlyCollection<Claim> claims)
        {
            var signingCredentials = GetSigningCredentials();

            var tokenOptions = new JwtSecurityToken(
                issuer: _jwtSettings.ValidIssuer,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_jwtSettings.ExpiryInMinutes)),
                signingCredentials: signingCredentials);

            var token = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

            return token;
        }

        private SigningCredentials GetSigningCredentials()
        {

            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecurityKey);
            var secret = new SymmetricSecurityKey(key);

            return new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);
        }
    }
}
