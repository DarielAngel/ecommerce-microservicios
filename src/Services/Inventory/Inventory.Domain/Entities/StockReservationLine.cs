namespace Ecommerce.Inventory.Domain.Entities;

public class StockReservationLine
{
    public Guid Id { get; private set; }
    public Guid StockReservationId { get; private set; }
    public Guid VariantId { get; private set; }
    public int Quantity { get; private set; }

    private StockReservationLine() { }

    internal StockReservationLine(Guid stockReservationId, Guid variantId, int quantity)
    {
        Id = Guid.NewGuid();
        StockReservationId = stockReservationId;
        VariantId = variantId;
        Quantity = quantity;
    }
}
