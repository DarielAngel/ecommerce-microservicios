using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly CatalogDbContext _context;

    public CategoryRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct)
    {
        var slug = Category.Slugify(name.Trim());
        return _context.Categories.AnyAsync(c => c.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<Category>> ListAllAsync(CancellationToken ct) =>
        await _context.Categories.OrderBy(c => c.Name).ToListAsync(ct);

    public async Task AddAsync(Category category, CancellationToken ct) =>
        await _context.Categories.AddAsync(category, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}
