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

        // La tienda nunca muestra productos desactivados; el panel de Admin sí, para poder reactivarlos.
        if (!filter.IncludeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            // unaccent(): "audifonos" encuentra "Audífonos" y "cafe" encuentra "Café".
            var pattern = $"%{EscapeLike(filter.SearchTerm.Trim())}%";
            query = query.Where(p =>
                EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(pattern)) ||
                EF.Functions.ILike(EF.Functions.Unaccent(p.Description), EF.Functions.Unaccent(pattern)));
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

        query = filter.SortBy switch
        {
            "price_asc" => query.OrderBy(p => p.Variants.Min(v => v.Price)),
            "price_desc" => query.OrderByDescending(p => p.Variants.Min(v => v.Price)),
            "newest" => query.OrderByDescending(p => p.CreatedAtUtc),
            _ => query.OrderBy(p => p.Name) // "name" o sin especificar: comportamiento de siempre
        };

        var items = await ToSummaries(query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize))
            .ToListAsync(ct);

        return new PagedResult<ProductSummary>(items, filter.Page, filter.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<ProductSummary>> SuggestAsync(string term, int limit, CancellationToken ct)
    {
        var clean = EscapeLike(term.Trim());
        var contains = $"%{clean}%";
        var startsWith = $"{clean}%";

        var query = _context.Products
            .Where(p => p.IsActive && EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(contains)))
            // Primero los que EMPIEZAN con lo escrito ("gal" → "Galaxy..." antes que "Samsung Galaxy..."),
            // después el resto; dentro de cada grupo, por nombre.
            .OrderBy(p => EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(startsWith)) ? 0 : 1)
            .ThenBy(p => p.Name)
            .Take(limit);

        return await ToSummaries(query).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductSummary>> GetRelatedAsync(Guid productId, int limit, CancellationToken ct)
    {
        var source = await _context.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new { p.CategoryId })
            .FirstOrDefaultAsync(ct);
        if (source is null) return Array.Empty<ProductSummary>();

        var parentId = await _context.Categories.AsNoTracking()
            .Where(c => c.Id == source.CategoryId)
            .Select(c => c.ParentCategoryId)
            .FirstOrDefaultAsync(ct);

        // Misma categoría primero; si hay padre, también las categorías hermanas.
        var categoryIds = parentId is null
            ? new List<Guid> { source.CategoryId }
            : await _context.Categories.AsNoTracking()
                .Where(c => c.Id == source.CategoryId || c.ParentCategoryId == parentId)
                .Select(c => c.Id)
                .ToListAsync(ct);

        var query = _context.Products
            .Where(p => p.IsActive && p.Id != productId && categoryIds.Contains(p.CategoryId))
            .OrderBy(p => p.CategoryId == source.CategoryId ? 0 : 1)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Take(limit);

        return await ToSummaries(query).ToListAsync(ct);
    }

    private IQueryable<ProductSummary> ToSummaries(IQueryable<Product> query)
    {
        var categoriesQuery = _context.Categories;
        return query.Select(p => new ProductSummary(
            p.Id,
            p.Name,
            p.Slug,
            p.CategoryId,
            categoriesQuery.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
            p.Variants.Any() ? p.Variants.Min(v => v.Price) : (decimal?)null,
            p.Images.Where(i => i.IsPrimary).Select(i => i.FileName).FirstOrDefault(),
            p.IsActive));
    }

    /// <summary>Lo que el cliente escribe es texto, no un patrón: "%" y "_" se buscan tal cual.</summary>
    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    public async Task AddAsync(Product product, CancellationToken ct) =>
        await _context.Products.AddAsync(product, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}
