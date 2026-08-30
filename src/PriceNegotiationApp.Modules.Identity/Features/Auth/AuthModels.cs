namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

public sealed record CurrentUserResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);
