using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Responses;

namespace PriceNegotiationApp.Application.Features.Auth;

public sealed class AuthService(IUserAccountStore accounts, IJwtTokenGenerator jwt) : IAuthService
{
    public async Task<RegistrationResponse> RegisterAsync(string email, string password, CancellationToken ct)
    {
        var outcome = await accounts.RegisterAsync(email, password, ct);
        if (!outcome.Succeeded)
        {
            throw outcome.EmailAlreadyTaken
                ? new ConflictException(ErrorCodes.EmailAlreadyRegistered,
                    outcome.ErrorDescription ?? "Email already registered.")
                : new InvalidRequestException(ErrorCodes.RegistrationInvalid,
                    outcome.ErrorDescription ?? "Registration failed.");
        }

        return new RegistrationResponse(outcome.UserId);
    }

    public async Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct)
    {
        var signIn = await accounts.PasswordSignInAsync(email, password);
        switch (signIn)
        {
            case SignInResultKind.LockedOut:
                throw new UnauthorizedException(ErrorCodes.AccountLocked, "Account temporarily locked.");
            case SignInResultKind.Failure:
                throw new UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid credentials.");
        }

        var userId = await accounts.ResolveUserIdByEmailAsync(email, ct);
        var roles = await accounts.GetRolesAsync(userId, ct);
        var (token, expiresAtUtc) = await jwt.GenerateAsync(userId, email, roles);
        return new AuthResponse(token, expiresAtUtc, email, roles);
    }

    public CurrentUserResponse CurrentUserAsync(CallerContext caller) =>
        new(caller.UserId, caller.Email, caller.Roles.ToList());
}
