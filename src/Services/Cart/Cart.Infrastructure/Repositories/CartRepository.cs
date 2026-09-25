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
}
