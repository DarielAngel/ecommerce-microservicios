using System.Text.Json;
using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Búsqueda sin distinguir tildes (ver ProductRepository). EnsureCreated la instala en bases nuevas;
        // para bases que ya existían, Program.cs la crea al arrancar.
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            entity.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(160).IsRequired();
            entity.HasIndex(c => c.Slug).IsUnique();
            entity.Property(c => c.ParentCategoryId).HasColumnName("parent_category_id");
            entity.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasColumnName("description").IsRequired();
            entity.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(220).IsRequired();
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();
            entity.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            // Variantes e imágenes viven en tablas propias pero solo se acceden a través
            // del agregado Product (encapsulación real del dominio).
            entity.OwnsMany(p => p.Variants, variant =>
            {
                variant.ToTable("product_variants");
                variant.WithOwner().HasForeignKey(v => v.ProductId);
                variant.HasKey(v => v.Id);

                variant.Property(v => v.Sku).HasColumnName("sku").HasMaxLength(64).IsRequired();
                variant.HasIndex(v => v.Sku).IsUnique();
                variant.Property(v => v.Price).HasColumnName("price").HasColumnType("numeric(12,2)").IsRequired();
                variant.Property(v => v.IsActive).HasColumnName("is_active").IsRequired();
                variant.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

                // Atributos libres (talla, color, etc.) guardados como JSONB.
                variant.Property(v => v.Attributes)
                    .HasColumnName("attributes")
                    .HasColumnType("jsonb")
                    .HasConversion(
                        dict => JsonSerializer.Serialize(dict, (JsonSerializerOptions?)null),
                        json => (IReadOnlyDictionary<string, string>)(
                            JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null)
                                ?? new Dictionary<string, string>()))
                    .Metadata.SetValueComparer(new ValueComparer<IReadOnlyDictionary<string, string>>(
                        (left, right) => (left ?? new Dictionary<string, string>())
                            .OrderBy(kv => kv.Key)
                            .SequenceEqual((right ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key)),
                        dict => dict.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
                        dict => new Dictionary<string, string>(dict)));
            });

            entity.OwnsMany(p => p.Images, image =>
            {
                image.ToTable("product_images");
                image.WithOwner().HasForeignKey(i => i.ProductId);
                image.HasKey(i => i.Id);

                image.Property(i => i.FileName).HasColumnName("file_name").IsRequired();
                image.Property(i => i.IsPrimary).HasColumnName("is_primary").IsRequired();
                image.Property(i => i.DisplayOrder).HasColumnName("display_order").IsRequired();
                image.Property(i => i.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            });

            entity.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
