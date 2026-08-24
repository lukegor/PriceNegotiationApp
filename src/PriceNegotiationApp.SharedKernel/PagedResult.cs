namespace PriceNegotiationApp.SharedKernel;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
