using Ecommerce.Catalog.Domain.Exceptions;

namespace Ecommerce.Catalog.Domain.Entities;

public class Product
{
    private readonly List<ProductVariant> _variants = new();
    private readonly List<ProductImage> _images = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    private Product() { }

    private Product(string name, string description, Guid categoryId)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Slug = Category.Slugify(name) + "-" + Id.ToString("N")[..8];
        CategoryId = categoryId;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Product Create(string name, string description, Guid categoryId)
    {
        ValidateName(name);

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("La descripción del producto es obligatoria.");
        }

        return new Product(name.Trim(), description.Trim(), categoryId);
    }

    public ProductVariant AddVariant(string sku, decimal price, IDictionary<string, string> attributes)
    {
        if (_variants.Any(v => v.Sku == sku.Trim().ToUpperInvariant()))
        {
            throw new DomainException($"Ya existe una variante con el SKU '{sku}' en este producto.");
        }

        var variant = new ProductVariant(Id, sku, price, attributes);
        _variants.Add(variant);
        return variant;
    }

    public ProductImage AddImage(string fileName, bool isPrimary)
    {
        if (isPrimary)
        {
            // Solo puede haber una imagen principal: las demás se degradan.
            foreach (var existingImage in _images)
            {
                existingImage.UnsetAsPrimary();
            }
        }

        var image = new ProductImage(Id, fileName, isPrimary || _images.Count == 0, _images.Count);
        _images.Add(image);
        return image;
    }

    public void UpdateDetails(string name, string description, Guid categoryId)
    {
        ValidateName(name);

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("La descripción del producto es obligatoria.");
        }

        Name = name.Trim();
        Description = description.Trim();
        CategoryId = categoryId;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            throw new DomainException("El nombre del producto debe tener al menos 2 caracteres.");
        }
    }
}
