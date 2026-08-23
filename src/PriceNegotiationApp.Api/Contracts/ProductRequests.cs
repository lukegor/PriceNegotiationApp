using PriceNegotiationApp.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace PriceNegotiationApp.Api.Contracts;

public sealed class CreateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [Range(0.01, 999_999_999)]
    public decimal Price { get; init; }
}

public sealed class UpdateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [Range(0.01, 999_999_999)]
    public decimal Price { get; init; }
}

public sealed class ProductListRequest
{
    public string? Search { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public string? SortBy { get; init; }

    public bool SortDesc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public ProductQuery ToQuery() => new(Search, MinPrice, MaxPrice, SortBy, SortDesc, Page, PageSize);
}
