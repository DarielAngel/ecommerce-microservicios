using Ecommerce.Inventory.Domain.Exceptions;

namespace Ecommerce.Inventory.Domain.Entities;

/// <summary>
/// Lleva la cuenta de stock de UNA variante de producto (referenciada por VariantId, un Guid
/// que vive en la base de datos de Catálogo — Inventario no tiene FK real hacia ella, solo
/// una referencia lógica, tal como corresponde a "Database per Service").
///
/// Los incrementos/decrementos de QuantityReserved durante una reserva de checkout NO pasan
/// por esta entidad: se hacen con una actualización SQL atómica en el repositorio, para evitar
/// condiciones de carrera cuando dos clientes compran el último ítem al mismo tiempo. Esta
/// entidad se usa para lecturas y para ajustes manuales del Admin (donde la carrera no es un problema).
/// </summary>
public class StockItem
{
    public Guid VariantId { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public int LowStockThreshold { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public int QuantityAvailable => QuantityOnHand - QuantityReserved;
    public bool IsLowStock => QuantityAvailable <= LowStockThreshold;

    private StockItem() { }

    private StockItem(Guid variantId)
    {
        VariantId = variantId;
        QuantityOnHand = 0;
        QuantityReserved = 0;
        LowStockThreshold = 5;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static StockItem CreateEmpty(Guid variantId) => new(variantId);

    /// <summary>Ajuste manual de un Admin (recepción de mercadería, corrección de inventario físico, etc.)</summary>
    public void SetQuantityOnHand(int newQuantityOnHand)
    {
        if (newQuantityOnHand < 0)
        {
            throw new DomainException("La cantidad en stock no puede ser negativa.");
        }

        if (newQuantityOnHand < QuantityReserved)
        {
            throw new DomainException(
                $"No se puede fijar el stock en {newQuantityOnHand} porque hay {QuantityReserved} unidades ya reservadas.");
        }

        QuantityOnHand = newQuantityOnHand;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetLowStockThreshold(int threshold)
    {
        if (threshold < 0)
        {
            throw new DomainException("El umbral de bajo stock no puede ser negativo.");
        }

        LowStockThreshold = threshold;
    }
}
