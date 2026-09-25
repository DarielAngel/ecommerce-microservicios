using Ecommerce.Catalog.Domain.Exceptions;

namespace Ecommerce.Catalog.Domain.Entities;

public class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public Guid? ParentCategoryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Category() { }

    private Category(string name, string slug, Guid? parentCategoryId)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
        ParentCategoryId = parentCategoryId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Category Create(string name, Guid? parentCategoryId = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            throw new DomainException("El nombre de la categoría debe tener al menos 2 caracteres.");
        }

        var trimmedName = name.Trim();
        return new Category(trimmedName, Slugify(trimmedName), parentCategoryId);
    }

    public static string Slugify(string value) =>
        string.Join('-', value
                .Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Replace("ñ", "n")
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");
}
