namespace Ecommerce.Orders.Domain.Entities;

public class OrderLine
{
    public Guid Id { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private OrderLine() { }

    internal OrderLine(Guid variantId, Guid productId, string productName, string sku, decimal unitPrice, int quantity)
    {
        Id = Guid.NewGuid();
        VariantId = variantId;
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}
