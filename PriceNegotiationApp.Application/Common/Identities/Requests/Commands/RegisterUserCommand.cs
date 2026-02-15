namespace PriceNegotiationApp.Application.Common.Identities.Requests.Commands
{
    public record RegisterUserCommand(
       string UserName,
       string Name,
       string Email,
       string? StreetAddress,
       string? City,
       string? State,
       string? PostalCode,
       string Password,
       string ConfirmPassword);
}
