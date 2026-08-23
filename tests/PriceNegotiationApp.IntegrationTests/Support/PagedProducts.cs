namespace PriceNegotiationApp.IntegrationTests.Support;

public sealed record PagedProducts(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, long TotalCount);
