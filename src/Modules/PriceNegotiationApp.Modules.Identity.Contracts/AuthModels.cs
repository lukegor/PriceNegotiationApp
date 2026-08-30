namespace PriceNegotiationApp.Modules.Identity.Contracts;

public sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
