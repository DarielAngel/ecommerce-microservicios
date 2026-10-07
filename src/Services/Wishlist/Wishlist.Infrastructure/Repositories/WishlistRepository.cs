using Ecommerce.Wishlist.Application.Common;
using Ecommerce.Wishlist.Domain.Entities;
using Ecommerce.Wishlist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Wishlist.Infrastructure.Repositories;

public class WishlistRepository : IWishlistRepository
{
    private readonly WishlistDbContext _context;

    public WishlistRepository(WishlistDbContext context) => _context = context;

    public Task<bool> ExistsAsync(Guid userId, Guid productId, CancellationToken ct) =>
        _context.Items.AnyAsync(i => i.UserId == userId && i.ProductId == productId, ct);

    public Task<int> CountAsync(Guid userId, CancellationToken ct) =>
        _context.Items.CountAsync(i => i.UserId == userId, ct);

    public async Task AddIfMissingAsync(WishlistItem item, CancellationToken ct)
    {
        // Un INSERT normal fallaría con violación de clave si dos requests llegan a la vez;
        // ON CONFLICT DO NOTHING lo resuelve en la base de datos, en una sola sentencia.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO wishlist_items (user_id, product_id, added_at_utc)
             VALUES ({item.UserId}, {item.ProductId}, {item.AddedAtUtc})
             ON CONFLICT (user_id, product_id) DO NOTHING
             """, ct);
    }

    public async Task RemoveAsync(Guid userId, Guid productId, CancellationToken ct) =>
        await _context.Items
            .Where(i => i.UserId == userId && i.ProductId == productId)
            .ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<WishlistItem>> ListAsync(Guid userId, CancellationToken ct) =>
        await _context.Items.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.AddedAtUtc)
            .ToListAsync(ct);
}
