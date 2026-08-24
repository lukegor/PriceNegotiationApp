using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Modules.Identity.Auth;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Login
{
    internal static void MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async (LoginRequest request, UserManager<ApplicationUser> userManager,
                JwtManager jwt, CancellationToken ct) =>
            {
                var user = await userManager.FindByNameAsync(request.Email)
                           ?? throw new UnauthorizedException(
                               IdentityErrorCodes.InvalidCredentials, "Invalid credentials.");

                if (await userManager.IsLockedOutAsync(user))
                {
                    throw new UnauthorizedException(IdentityErrorCodes.AccountLocked,
                        "Account temporarily locked.");
                }

                if (!await userManager.CheckPasswordAsync(user, request.Password))
                {
                    await userManager.AccessFailedAsync(user);
                    throw await userManager.IsLockedOutAsync(user)
                        ? new UnauthorizedException(IdentityErrorCodes.AccountLocked,
                            "Account temporarily locked.")
                        : new UnauthorizedException(IdentityErrorCodes.InvalidCredentials,
                            "Invalid credentials.");
                }

                await userManager.ResetAccessFailedCountAsync(user);

                var roles = (IReadOnlyList<string>)await userManager.GetRolesAsync(user);
                var (token, expiresAtUtc) = await jwt.GenerateAsync(user.Id, request.Email, roles);
                return TypedResults.Ok(new AuthResponse(token, expiresAtUtc, request.Email, roles));
            })
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous();
    }
}
