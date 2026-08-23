namespace PriceNegotiationApp.Application.Responses;

public sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
