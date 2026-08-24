using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.BuildingBlocks;
using PriceNegotiationApp.Domain.Models;

namespace PriceNegotiationApp.Infrastructure.Identity;

public sealed class IdentityAccountStore(UserManager<ApplicationUser> userManager) : IUserAccountStore
{
    public async Task<RegistrationOutcome> RegisterAsync(string email, string password, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                ? RegistrationOutcome.DuplicateEmail()
                : RegistrationOutcome.ValidationFailed(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, UserRoles.Customer);
        return RegistrationOutcome.Success(user.Id);
    }

    public async Task<SignInResultKind> PasswordSignInAsync(string email, string password)
    {
        var user = await userManager.FindByNameAsync(email);
        if (user is null)
        {
            return SignInResultKind.Failure;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return SignInResultKind.LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user)
                ? SignInResultKind.LockedOut
                : SignInResultKind.Failure;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return SignInResultKind.Success;
    }

    public async Task<Guid> ResolveUserIdByEmailAsync(string email, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email)
                   ?? throw new NotFoundException(nameof(ApplicationUser), email);
        return user.Id;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        return (IReadOnlyList<string>)await userManager.GetRolesAsync(user);
    }
}


