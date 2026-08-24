namespace PriceNegotiationApp.BuildingBlocks;

public sealed record ProductQuery(
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? SortBy = null,
    bool SortDesc = false,
    int Page = 1,
    int PageSize = 20);
