namespace PriceNegotiationApp.Infrastructure.Auth.Authentication.Jwt
{
    public class JwtSettings
    {
        public string SecurityKey { get; set; }
        public string ValidIssuer { get; set; }
        public int ExpiryInMinutes { get; set; }
    }
}
