namespace PriceNegotiationApp.Contracts.Identities.Dtos.Responses
{
    public record AuthResponseDto(
        bool IsAuthSuccessful,
        string? ErrorMessage,
        string? Token);
}
