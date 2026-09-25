using Ecommerce.Catalog.Domain.Exceptions;

namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// Cada variante tiene su propio SKU y precio. Los atributos (talla, color, etc.)
/// son libres a propósito: así no hay que tocar el esquema si mañana aparece
/// un atributo nuevo (ej. "Material"). El STOCK de esta variante vive en el
/// futuro microservicio de Inventario, referenciado por Id — Catálogo no lo guarda.
/// </summary>
public class ProductVariant
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = null!;
    public decimal Price { get; private set; }
    public IReadOnlyDictionary<string, string> Attributes { get; private set; } = new Dictionary<string, string>();
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ProductVariant() { }

    internal ProductVariant(Guid productId, string sku, decimal price, IDictionary<string, string> attributes)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("El SKU de la variante es obligatorio.");
        }

        if (price <= 0)
        {
            throw new DomainException("El precio de la variante debe ser mayor a cero.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        Sku = sku.Trim().ToUpperInvariant();
        Price = price;
        Attributes = new Dictionary<string, string>(attributes);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
        {
            throw new DomainException("El precio de la variante debe ser mayor a cero.");
        }

        Price = newPrice;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
