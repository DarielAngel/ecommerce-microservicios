using Ecommerce.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Orders.Infrastructure.Persistence;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");
            order.HasKey(o => o.Id);

            order.Property(o => o.UserId).HasColumnName("user_id").IsRequired();
            order.HasIndex(o => o.UserId);

            order.Property(o => o.ShippingAddress).HasColumnName("shipping_address").HasMaxLength(500).IsRequired();
            order.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            order.Property(o => o.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);

            order.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            order.Property(o => o.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            order.Property(o => o.PaidAtUtc).HasColumnName("paid_at_utc");

            order.OwnsMany(o => o.Lines, line =>
            {
                line.ToTable("order_lines");
                line.WithOwner().HasForeignKey("OrderId");
                line.HasKey(l => l.Id);

                line.Property(l => l.VariantId).HasColumnName("variant_id").IsRequired();
                line.Property(l => l.ProductId).HasColumnName("product_id").IsRequired();
                line.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
                line.Property(l => l.Sku).HasColumnName("sku").HasMaxLength(100).IsRequired();
                line.Property(l => l.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)").IsRequired();
                line.Property(l => l.Quantity).HasColumnName("quantity").IsRequired();
            });

            order.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
