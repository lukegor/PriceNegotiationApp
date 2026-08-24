using Microsoft.EntityFrameworkCore;
using PriceNegotiationApp.Application.Abstractions;
using PriceNegotiationApp.Application.Common;
using PriceNegotiationApp.Application.Exceptions;
using PriceNegotiationApp.Application.Responses;
using PriceNegotiationApp.Domain.Models;
using PriceNegotiationApp.Domain.ValueObjects.Ids;

namespace PriceNegotiationApp.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> GetAsync(ProductId id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public IQueryable<Product> Query() => db.Products.AsNoTracking();

    public async Task AddAsync(Product product, CancellationToken ct) =>
        await db.Products.AddAsync(product, ct);

    public void Remove(Product product) => db.Products.Remove(product);

    public async Task<PagedResult<ProductResponse>> SearchAsync(ProductQuery query, CancellationToken ct)
    {
        var page = new PageQuery(query.Page, query.PageSize);
        var q = Query();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            q = q.Where(p => EF.Functions.ILike(p.Name, $"%{query.Search.Trim()}%"));
        }

        if (query.MinPrice.HasValue)
        {
            q = q.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            q = q.Where(p => p.Price <= query.MaxPrice.Value);
        }

        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        q = (sortBy, query.SortDesc) switch
        {
            ("price", false) => q.OrderBy(p => p.Price),
            ("price", true) => q.OrderByDescending(p => p.Price),
            (_, true) => q.OrderByDescending(p => p.Name),
            _ => q.OrderBy(p => p.Name),
        };

        var total = await q.LongCountAsync(ct);
        var items = await q
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .Select(p => new ProductResponse(p.Id.Value, p.Name, p.Price))
            .ToListAsync(ct);

        return new PagedResult<ProductResponse>(items, page.SafePage, page.SafePageSize, total);
    }
}



