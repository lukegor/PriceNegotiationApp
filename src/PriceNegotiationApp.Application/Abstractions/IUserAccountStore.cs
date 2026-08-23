namespace PriceNegotiationApp.Application.Abstractions;

public interface IUserAccountStore
{
    Task<RegistrationOutcome> RegisterAsync(string email, string password, CancellationToken ct);

    Task<SignInResultKind> PasswordSignInAsync(string email, string password);

    Task<Guid> ResolveUserIdByEmailAsync(string email, CancellationToken ct);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct);
}
