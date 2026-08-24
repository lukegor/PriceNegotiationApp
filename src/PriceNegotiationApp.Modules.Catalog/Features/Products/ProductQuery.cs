namespace PriceNegotiationApp.Modules.Catalog.Features.Products;

internal sealed record ProductQuery(
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? SortBy = null,
    bool SortDesc = false,
    int Page = 1,
    int PageSize = 20);
