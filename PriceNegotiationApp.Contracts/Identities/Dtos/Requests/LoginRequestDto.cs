namespace PriceNegotiationApp.Contracts.Identities.Dtos.Requests
{
    public class LoginRequestDto
    {
        public required string Username { get; init; }
        public required string Password { get; init; }
    }
}
