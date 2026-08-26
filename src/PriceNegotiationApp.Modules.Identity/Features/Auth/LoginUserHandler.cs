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
                   ?? throw Unauthorized();

        // Lockout keeps enforcing internally but reads identically to any other failure.
        if (await userManager.IsLockedOutAsync(user))
        {
            throw Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw Unauthorized();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = (IReadOnlyList<string>)await userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = jwt.Generate(user.Id, request.Email, roles);
        return new AuthResponse(token, expiresAtUtc, request.Email, roles);
    }

    private static UnauthorizedException Unauthorized() =>
        new(IdentityErrorCodes.InvalidCredentials, "Invalid credentials.");
}
