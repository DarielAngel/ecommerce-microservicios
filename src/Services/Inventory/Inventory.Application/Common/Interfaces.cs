using Ecommerce.Inventory.Domain.Entities;

namespace Ecommerce.Inventory.Application.Common;

public interface IStockItemRepository
{
    Task<StockItem?> GetByVariantIdAsync(Guid variantId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid variantId, CancellationToken ct);
    Task AddAsync(StockItem stockItem, CancellationToken ct);
    Task<IReadOnlyList<StockItem>> ListLowStockAsync(CancellationToken ct);

    /// <summary>Los registros de stock de varias variantes en una sola consulta (las que no existen se omiten).</summary>
    Task<IReadOnlyList<StockItem>> GetByVariantIdsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct);

    /// <summary>
    /// Reserva stock de forma ATÓMICA a nivel de base de datos (UPDATE ... WHERE disponible >= cantidad),
    /// para evitar condiciones de carrera entre checkouts concurrentes. Devuelve true si había
    /// disponibilidad suficiente y se reservó; false si no (0 filas afectadas = sin stock suficiente).
    /// </summary>
    Task<bool> TryReserveAsync(Guid variantId, int quantity, CancellationToken ct);

    /// <summary>Confirma una reserva ya hecha: descuenta definitivamente QuantityOnHand y QuantityReserved.</summary>
    Task ConfirmReservedAsync(Guid variantId, int quantity, CancellationToken ct);

    /// <summary>Libera una reserva: solo descuenta QuantityReserved, QuantityOnHand queda igual.</summary>
    Task ReleaseReservedAsync(Guid variantId, int quantity, CancellationToken ct);

    /// <summary>
    /// Vuelve a sumar al stock las unidades de una devolución reembolsada (Fase 7), UNA sola vez por devolución y
    /// variante: registra (devolución, variante) y suma en la misma sentencia. Devuelve false si ya se había sumado.
    /// </summary>
    Task<bool> RestockReturnedAsync(Guid returnId, Guid variantId, int quantity, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

public interface IStockReservationRepository
{
    Task<StockReservation?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task AddAsync(StockReservation reservation, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>
/// Abstrae una transacción de base de datos que abarca varias operaciones atómicas
/// (varias líneas de una reserva deben reservarse todas o ninguna).
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken ct);
}
