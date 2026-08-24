namespace PriceNegotiationApp.SharedKernel;

public sealed record CallerContext(Guid UserId, string Email, IReadOnlySet<string> Roles)
{
    private static readonly IReadOnlySet<string> EmptyRoles = new HashSet<string>();

    public static readonly CallerContext Anonymous = new(Guid.Empty, string.Empty, EmptyRoles);

    public bool IsAuthenticated => UserId != Guid.Empty;

    public bool IsInRole(string role) => Roles.Contains(role);
}
