using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Identity.Persistence;
using PriceNegotiationApp.Modules.Identity.Public;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

internal static class Register
{
    internal static void MapRegister(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterRequest request,
                UserManager<ApplicationUser> userManager, CancellationToken ct) =>
            {
                var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
                IdentityResult result;
                try
                {
                    result = await userManager.CreateAsync(user, request.Password);
                }
                catch (DbUpdateException ex) when (DbWriteGuard.IsUniqueViolation(ex, out _))
                {
                    // Two concurrent registrations for the same email: Identity's pre-check
                    // lost the race, the unique index caught it — same conflict as usual.
                    throw new ConflictException(IdentityErrorCodes.EmailAlreadyRegistered,
                        "Email already registered.");
                }

                if (!result.Succeeded)
                {
                    if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                    {
                        throw new ConflictException(IdentityErrorCodes.EmailAlreadyRegistered,
                            "Email already registered.");
                    }

                    throw new InvalidRequestException(IdentityErrorCodes.RegistrationInvalid,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }

                await userManager.AddToRoleAsync(user, UserRoles.Customer);
                return TypedResults.Created("/api/v1/auth/me", new RegistrationResponse(user.Id));
            })
        .RequireRateLimiting(Policies.AuthRateLimitPolicy)
        .AllowAnonymous();
    }
}
