using Microsoft.AspNetCore.Identity;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Domain.Models;

namespace PriceNegotiationApp.Infrastructure.Identity;

public sealed class IdentityAccountStore(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : IUserAccountStore
{
    private static readonly RegistrationOutcome DuplicateEmail = new(false, Guid.Empty, "Email already registered.");

    public async Task<RegistrationOutcome> RegisterAsync(string email, string password, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                ? DuplicateEmail
                : ValidationFailed(result.Errors);
        }

        await userManager.AddToRoleAsync(user, UserRoles.Customer);
        return new RegistrationOutcome(true, user.Id, null);
    }

    public async Task<SignInResultKind> PasswordSignInAsync(string email, string password)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? SignInResultKind.Success
            : result.IsLockedOut ? SignInResultKind.LockedOut
            : SignInResultKind.Failure;
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

    private static RegistrationOutcome ValidationFailed(IEnumerable<IdentityError> errors) =>
        new(false, Guid.Empty, string.Join("; ", errors.Select(e => e.Description)));
}

