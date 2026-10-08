using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Repositories;

public class StockItemRepository : IStockItemRepository
{
    private readonly InventoryDbContext _context;

    public StockItemRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<StockItem?> GetByVariantIdAsync(Guid variantId, CancellationToken ct) =>
        _context.StockItems.FirstOrDefaultAsync(s => s.VariantId == variantId, ct);

    public Task<bool> ExistsAsync(Guid variantId, CancellationToken ct) =>
        _context.StockItems.AnyAsync(s => s.VariantId == variantId, ct);

    public async Task<IReadOnlyList<StockItem>> ListLowStockAsync(CancellationToken ct) =>
        await _context.StockItems
            .Where(s => s.QuantityOnHand - s.QuantityReserved <= s.LowStockThreshold)
            .OrderBy(s => s.QuantityOnHand - s.QuantityReserved)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StockItem>> GetByVariantIdsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        var ids = variantIds.ToList();
        return await _context.StockItems.AsNoTracking().Where(s => ids.Contains(s.VariantId)).ToListAsync(ct);
    }

    public async Task AddAsync(StockItem stockItem, CancellationToken ct) =>
        await _context.StockItems.AddAsync(stockItem, ct);

    public async Task<bool> TryReserveAsync(Guid variantId, int quantity, CancellationToken ct)
    {
        // UPDATE atómico a nivel de base de datos: solo reserva si hay disponibilidad suficiente
        // EN EL MISMO INSTANTE de la escritura. Esto es lo que evita la condición de carrera
        // de dos clientes comprando el último ítem al mismo tiempo (RNF de concurrencia del
        // documento de arquitectura) sin necesitar locks explícitos ni SELECT FOR UPDATE.
        var affected = await _context.StockItems
            .Where(s => s.VariantId == variantId && s.QuantityOnHand - s.QuantityReserved >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.QuantityReserved, s => s.QuantityReserved + quantity)
                .SetProperty(s => s.UpdatedAtUtc, DateTime.UtcNow), ct);

        return affected > 0;
    }

    public async Task ConfirmReservedAsync(Guid variantId, int quantity, CancellationToken ct)
    {
        await _context.StockItems
            .Where(s => s.VariantId == variantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.QuantityOnHand, s => s.QuantityOnHand - quantity)
                .SetProperty(s => s.QuantityReserved, s => s.QuantityReserved - quantity)
                .SetProperty(s => s.UpdatedAtUtc, DateTime.UtcNow), ct);
    }

    public async Task ReleaseReservedAsync(Guid variantId, int quantity, CancellationToken ct)
    {
        await _context.StockItems
            .Where(s => s.VariantId == variantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.QuantityReserved, s => s.QuantityReserved - quantity)
                .SetProperty(s => s.UpdatedAtUtc, DateTime.UtcNow), ct);
    }

    public async Task<bool> RestockReturnedAsync(Guid returnId, Guid variantId, int quantity, CancellationToken ct)
    {
        // Una sola sentencia: si la fila (devolución, variante) ya existía, el INSERT no devuelve nada y el UPDATE
        // no suma. Así un evento repetido (o dos copias a la vez) nunca suma dos veces.
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH inserted AS (
                INSERT INTO stock_restocks (return_id, variant_id, quantity, created_at_utc)
                VALUES ({returnId}, {variantId}, {quantity}, {DateTime.UtcNow})
                ON CONFLICT (return_id, variant_id) DO NOTHING
                RETURNING 1)
            UPDATE stock_items
               SET quantity_on_hand = quantity_on_hand + {quantity}, updated_at_utc = {DateTime.UtcNow}
             WHERE variant_id = {variantId} AND EXISTS (SELECT 1 FROM inserted)
            """, ct);
        return affected > 0;
    }

    public Task SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
