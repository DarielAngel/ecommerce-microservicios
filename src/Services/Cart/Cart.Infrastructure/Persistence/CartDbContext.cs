using CartAggregate = Ecommerce.Cart.Domain.Entities.Cart;
using Ecommerce.Cart.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Cart.Infrastructure.Persistence;

public class CartDbContext : DbContext
{
    public CartDbContext(DbContextOptions<CartDbContext> options) : base(options) { }

    public DbSet<CartAggregate> Carts => Set<CartAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CartAggregate>(cart =>
        {
            cart.ToTable("carts");
            cart.HasKey(c => c.Id);

            cart.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
            cart.HasIndex(c => c.UserId).IsUnique(); // un solo carrito por usuario

            cart.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            cart.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            cart.Property(c => c.ContactEmail).HasColumnName("contact_email").HasMaxLength(256);
            cart.Property(c => c.ContactName).HasColumnName("contact_name").HasMaxLength(200);
            cart.Property(c => c.AbandonedReminderForActivityAtUtc).HasColumnName("abandoned_reminder_for_activity_at_utc");

            cart.OwnsMany(c => c.Items, item =>
            {
                item.ToTable("cart_items");
                item.WithOwner().HasForeignKey("CartId");
                item.HasKey(i => i.Id);

                item.Property(i => i.VariantId).HasColumnName("variant_id").IsRequired();
                item.Property(i => i.ProductId).HasColumnName("product_id").IsRequired();
                item.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
                item.Property(i => i.Sku).HasColumnName("sku").HasMaxLength(100).IsRequired();
                item.Property(i => i.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)").IsRequired();
                item.Property(i => i.Quantity).HasColumnName("quantity").IsRequired();
                item.Property(i => i.AddedAtUtc).HasColumnName("added_at_utc").IsRequired();

                // Un usuario no puede tener la misma variante en dos líneas distintas del mismo carrito.
                item.HasIndex("CartId", nameof(CartItem.VariantId)).IsUnique();
            });

            cart.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
