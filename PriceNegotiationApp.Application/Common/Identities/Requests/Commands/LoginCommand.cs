namespace PriceNegotiationApp.Application.Common.Identities.Requests.Commands
{
    public record LoginCommand(
        string Username,
        string Password);
}
