using Ecommerce.Loyalty.Domain.Entities;

namespace Ecommerce.Loyalty.Application.Common;

public interface ILoyaltyRepository
{
    /// <summary>
    /// Bloquea la cuenta del cliente hasta el final de la transacción (la crea si no existía).
    /// Debe llamarse dentro de <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/>.
    /// </summary>
    Task LockAccountAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<LoyaltyEntry>> ListByUserAsync(Guid userId, CancellationToken ct);
    Task<LoyaltyEntry?> GetAsync(Guid orderId, LoyaltyEntryKind kind, CancellationToken ct);

    /// <summary>Todos los movimientos de una orden (para saber cuánto se ajustó ya por devoluciones).</summary>
    Task<IReadOnlyList<LoyaltyEntry>> ListByOrderAsync(Guid orderId, CancellationToken ct);
    Task AddAsync(LoyaltyEntry entry, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);

    /// <summary>Guarda; un movimiento repetido para la misma orden y tipo lanza ConflictAppException.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
