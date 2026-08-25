using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal sealed class LoginUserHandler(UserManager<ApplicationUser> userManager, JwtManager jwt)
{
    public async Task<AuthResponse> HandleAsync(LoginRequest request)
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
        var (token, expiresAtUtc) = jwt.Generate(user.Id, request.Email, roles);
        return new AuthResponse(token, expiresAtUtc, request.Email, roles);
    }
}
