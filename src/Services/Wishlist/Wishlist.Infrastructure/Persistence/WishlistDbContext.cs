using Ecommerce.Wishlist.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Wishlist.Infrastructure.Persistence;

public class WishlistDbContext : DbContext
{
    public WishlistDbContext(DbContextOptions<WishlistDbContext> options) : base(options) { }

    public DbSet<WishlistItem> Items => Set<WishlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WishlistItem>(item =>
        {
            item.ToTable("wishlist_items");

            // La clave (usuario, producto) es la garantía real de "un favorito por producto".
            item.HasKey(i => new { i.UserId, i.ProductId }).HasName("pk_wishlist_items");

            item.Property(i => i.UserId).HasColumnName("user_id");
            item.Property(i => i.ProductId).HasColumnName("product_id");
            item.Property(i => i.AddedAtUtc).HasColumnName("added_at_utc").IsRequired();

            // Para listar "mis favoritos" ordenados por fecha sin recorrer la tabla.
            item.HasIndex(i => new { i.UserId, i.AddedAtUtc }).HasDatabaseName("ix_wishlist_items_user_added");
        });
    }
}
