namespace Ecommerce.Inventory.Domain.Entities;

/// <summary>
/// Registro de que las unidades de una devolución ya volvieron al stock (Fase 7). Existe para que el mismo
/// reembolso nunca sume dos veces: la clave es (devolución, variante).
/// </summary>
public class StockRestock
{
    public Guid ReturnId { get; private set; }
    public Guid VariantId { get; private set; }
    public int Quantity { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private StockRestock() { }
}
