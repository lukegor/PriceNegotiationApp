using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Application.Responses;

namespace PriceNegotiationApp.Application.Features.Auth;

public interface IAuthService
{
    Task<RegistrationResponse> RegisterAsync(string email, string password, CancellationToken ct);

    Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct);

    CurrentUserResponse CurrentUserAsync(CallerContext caller);
}

