using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Domain.Entities;
using Ecommerce.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Orders.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public OrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct) =>
        _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public async Task<IReadOnlyList<Order>> ListByUserIdAsync(Guid userId, CancellationToken ct) =>
        await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Order>> ListAllAsync(int count, CancellationToken ct) =>
        await _context.Orders
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(count)
            .ToListAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct) =>
        await _context.Orders.AddAsync(order, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _context.SaveChangesAsync(ct);
}
