using PriceNegotiationApp.Modules.Identity.Contracts;
using PriceNegotiationApp.Modules.Identity.Application.Register;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Modules.Identity.Infrastructure.Persistence;
using PriceNegotiationApp.SharedKernel;

namespace PriceNegotiationApp.Modules.Identity.Infrastructure.Register;

internal sealed class RegisterUserHandler(UserManager<ApplicationUser> userManager)
{
    public async Task<RegistrationResponse> HandleAsync(RegisterRequest request)
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
        return new RegistrationResponse(user.Id);
    }
}
