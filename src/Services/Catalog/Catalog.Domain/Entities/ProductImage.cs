namespace Ecommerce.Catalog.Domain.Entities;

public class ProductImage
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }

    /// <summary>Nombre/ruta del archivo guardado por el servicio (no la ruta completa del disco).</summary>
    public string FileName { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ProductImage() { }

    internal ProductImage(Guid productId, string fileName, bool isPrimary, int displayOrder)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        FileName = fileName;
        IsPrimary = isPrimary;
        DisplayOrder = displayOrder;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void SetAsPrimary() => IsPrimary = true;
    public void UnsetAsPrimary() => IsPrimary = false;
}
