using Ecommerce.Cart.Application.Common;
using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;
using Ecommerce.Cart.Domain.Entities;
using Ecommerce.Cart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly CartDbContext _context;

    public CartRepository(CartDbContext context)
    {
        _context = context;
    }

    public Task<CartAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
        _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public async Task AddAsync(CartAggregate cart, CancellationToken ct) =>
        await _context.Carts.AddAsync(cart, ct);

    public void TrackNewItem(CartItem item) =>
        _context.Entry(item).State = EntityState.Added;

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<CartAggregate>> ListIdleAsync(
        DateTime updatedFrom, DateTime updatedUntil, int limit, CancellationToken ct) =>
        await _context.Carts
            .Where(c => c.UpdatedAtUtc >= updatedFrom && c.UpdatedAtUtc <= updatedUntil
                        && c.ContactEmail != null && c.Items.Any()
                        // Ya avisados por esta misma actividad: se filtran en SQL para que no ocupen el lote
                        // y tapen a carritos que sí necesitan el correo.
                        && (c.AbandonedReminderForActivityAtUtc == null
                            || c.AbandonedReminderForActivityAtUtc != c.UpdatedAtUtc))
            .OrderBy(c => c.UpdatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
}
