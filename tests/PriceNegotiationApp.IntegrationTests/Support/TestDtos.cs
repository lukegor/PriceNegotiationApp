namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed record MeResponse(Guid UserId, string Email, IReadOnlyList<string> Roles);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string Email, IReadOnlyList<string> Roles);

public sealed record ProductResponse(Guid Id, string Name, decimal Price);

public sealed record PagedProducts(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, long TotalCount);
