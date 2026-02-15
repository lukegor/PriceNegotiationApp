namespace PriceNegotiationApp.Application.Common.Identities.Dtos
{
    public class AuthResultDto
    {
        public bool IsAuthSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Token { get; set; }
    }
}
