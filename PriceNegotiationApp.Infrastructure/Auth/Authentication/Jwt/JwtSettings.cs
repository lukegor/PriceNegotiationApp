namespace PriceNegotiationApp.Infrastructure.Auth.Authentication.Jwt
{
    public class JwtSettings
    {
        /// <summary>
        /// Gets or sets the security key used for authentication and authorization purposes.
        /// </summary>
        /// <remarks>
        /// 1. Do not expose in client-side code.
        /// 2. Min length 32 characters (32 bytes) for HmacSha256 since JsonWebToken package version 8.0.
        /// </remarks>
        public string SecurityKey { get; set; }

        public string ValidIssuer { get; set; }

        public int ExpiryInMinutes { get; set; }
    }
}
