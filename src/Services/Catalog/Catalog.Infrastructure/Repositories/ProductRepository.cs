using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly CatalogDbContext _context;

    public ProductRepository(CatalogDbContext context)
    {
        _context = context;
    }

    // Las colecciones "owned" (Variants, Images) se cargan automáticamente
    // con el producto: no hace falta .Include() explícito.
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetByVariantIdAsync(Guid variantId, CancellationToken ct) =>
        _context.Products.FirstOrDefaultAsync(p => p.Variants.Any(v => v.Id == variantId), ct);

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return _context.Products.SelectMany(p => p.Variants).AnyAsync(v => v.Sku == normalized, ct);
    }

    public void TrackNewImage(ProductImage image) =>
        _context.Entry(image).State = EntityState.Added;

    public async Task<PagedResult<ProductSummary>> SearchAsync(ProductSearchFilter filter, CancellationToken ct)
    {
        var query = _context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var pattern = $"%{filter.SearchTerm.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Description, pattern));
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
        }

        if (filter.MinPrice.HasValue)
        {
            var min = filter.MinPrice.Value;
            query = query.Where(p => p.Variants.Any(v => v.Price >= min));
        }

        if (filter.MaxPrice.HasValue)
        {
            var max = filter.MaxPrice.Value;
            query = query.Where(p => p.Variants.Any(v => v.Price <= max));
        }

        var totalCount = await query.CountAsync(ct);

        var categoriesQuery = _context.Categories;

        query = filter.SortBy switch
        {
            "price_asc" => query.OrderBy(p => p.Variants.Min(v => v.Price)),
            "price_desc" => query.OrderByDescending(p => p.Variants.Min(v => v.Price)),
            "newest" => query.OrderByDescending(p => p.CreatedAtUtc),
            _ => query.OrderBy(p => p.Name) // "name" o sin especificar: comportamiento de siempre
        };

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new ProductSummary(
                p.Id,
                p.Name,
                p.Slug,
                p.CategoryId,
                categoriesQuery.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                p.Variants.Any() ? p.Variants.Min(v => v.Price) : (decimal?)null,
                p.Images.Where(i => i.IsPrimary).Select(i => i.FileName).FirstOrDefault(),
                p.IsActive))
            .ToListAsync(ct);

        return new PagedResult<ProductSummary>(items, filter.Page, filter.PageSize, totalCount);
    }

    public async Task AddAsync(Product product, CancellationToken ct) =>
        await _context.Products.AddAsync(product, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}
