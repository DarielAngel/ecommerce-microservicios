using Ecommerce.Orders.Application.Common;
using Ecommerce.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ecommerce.Orders.Infrastructure.Repositories;

/// <summary>
/// Abre una transacción y bloquea la fila de la orden (SELECT ... FOR UPDATE) hasta liberarla. Todo lo que
/// se guarde en el medio queda dentro de esa transacción y se confirma al liberar.
/// </summary>
public class OrderLock : IOrderLock
{
    private readonly OrdersDbContext _context;

    public OrderLock(OrdersDbContext context) => _context = context;

    public async Task<IAsyncDisposable> AcquireAsync(Guid orderId, CancellationToken ct)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM orders WHERE \"Id\" = {orderId} FOR UPDATE", ct);
        return new Releaser(transaction);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly IDbContextTransaction _transaction;

        public Releaser(IDbContextTransaction transaction) => _transaction = transaction;

        public async ValueTask DisposeAsync()
        {
            // Se confirma lo que se haya guardado (igual que antes, sin transacción) y se libera el bloqueo.
            await _transaction.CommitAsync(CancellationToken.None);
            await _transaction.DisposeAsync();
        }
    }
}
