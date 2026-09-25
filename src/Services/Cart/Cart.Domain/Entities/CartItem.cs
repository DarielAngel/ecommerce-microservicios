namespace Ecommerce.Cart.Domain.Entities;

/// <summary>
/// Una línea del carrito. El precio queda "congelado" al momento de agregarse
/// (decisión de producto): si el precio cambia en Catálogo después, esta línea
/// no se actualiza sola. Nombre/SKU también son una copia (snapshot) para poder
/// mostrar el carrito sin tener que llamar a Catálogo en cada lectura.
/// </summary>
public class CartItem
{
    public Guid Id { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public DateTime AddedAtUtc { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private CartItem() { }

    internal CartItem(Guid variantId, Guid productId, string productName, string sku, decimal unitPrice, int quantity)
    {
        Id = Guid.NewGuid();
        VariantId = variantId;
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        Quantity = quantity;
        AddedAtUtc = DateTime.UtcNow;
    }

    internal void IncreaseQuantity(int amount) => Quantity += amount;

    internal void SetQuantity(int quantity) => Quantity = quantity;
}
