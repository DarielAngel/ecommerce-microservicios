using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Repositories;

public class StockReservationRepository : IStockReservationRepository
{
    private readonly InventoryDbContext _context;

    public StockReservationRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<StockReservation?> GetByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        _context.StockReservations.FirstOrDefaultAsync(r => r.OrderId == orderId, ct);

    public Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        _context.StockReservations.AnyAsync(r => r.OrderId == orderId, ct);

    public async Task AddAsync(StockReservation reservation, CancellationToken ct) =>
        await _context.StockReservations.AddAsync(reservation, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}

/// <summary>
/// Envuelve varias operaciones (reservar N líneas, o confirmar/liberar N líneas) en una única
/// transacción de base de datos: o se aplican todas, o ninguna.
/// </summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly InventoryDbContext _context;

    public EfUnitOfWork(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken ct)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await operation(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
