namespace PriceNegotiationApp.Application.Common.Identities.Dtos.Requests.Login
{
    public class LoginRequestDto
    {
        public string Username { get; }
        public string Password { get; }

        public LoginRequestDto(string username, string password)
        {
            Username = username;
            Password = password;
        }
    }
}
