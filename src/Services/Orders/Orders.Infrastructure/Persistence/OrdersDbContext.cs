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

            order.Property(o => o.UserEmail).HasColumnName("user_email").HasMaxLength(256).IsRequired();
            order.Property(o => o.UserFullName).HasColumnName("user_full_name").HasMaxLength(200).IsRequired();

            order.Property(o => o.ShippingAddress).HasColumnName("shipping_address").HasMaxLength(500).IsRequired();
            order.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            order.Property(o => o.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);

            order.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            order.Property(o => o.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            order.Property(o => o.PaidAtUtc).HasColumnName("paid_at_utc");
            order.Property(o => o.ShippedAtUtc).HasColumnName("shipped_at_utc");
            order.Property(o => o.CancelledAtUtc).HasColumnName("cancelled_at_utc");
            order.Ignore(o => o.EstimatedDelivery);
            order.Property(o => o.CouponCode).HasColumnName("coupon_code").HasMaxLength(30);
            order.Property(o => o.DiscountAmount).HasColumnName("discount_amount").HasColumnType("numeric(12,2)").IsRequired();
            order.Property(o => o.LoyaltyPoints).HasColumnName("loyalty_points").IsRequired();
            order.Property(o => o.LoyaltyDiscount).HasColumnName("loyalty_discount").HasColumnType("numeric(12,2)").IsRequired();

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

            order.Ignore(o => o.RefundedAmount);
            order.Ignore(o => o.ReturnDeadlineUtc);
            order.Ignore(o => o.OpenReturn);

            // Fase 7: devoluciones (cada una con sus líneas), dentro del agregado de la orden.
            order.OwnsMany(o => o.Returns, ret =>
            {
                ret.ToTable("order_returns");
                ret.WithOwner().HasForeignKey("OrderId");
                ret.Property<Guid>("OrderId").HasColumnName("order_id");
                ret.HasKey(r => r.Id);
                ret.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
                ret.Property(r => r.IsCancellation).HasColumnName("is_cancellation").IsRequired();
                ret.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
                ret.Property(r => r.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(30).IsRequired();
                ret.Property(r => r.Comment).HasColumnName("comment").HasMaxLength(500);
                ret.Property(r => r.AdminNote).HasColumnName("admin_note").HasMaxLength(500);
                ret.Property(r => r.RefundAmount).HasColumnName("refund_amount").HasColumnType("numeric(12,2)").IsRequired();
                ret.Property(r => r.LoyaltyPointsToRestore).HasColumnName("loyalty_points_to_restore").IsRequired();
                ret.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                ret.Property(r => r.ResolvedAtUtc).HasColumnName("resolved_at_utc");
                ret.Property(r => r.RefundedAtUtc).HasColumnName("refunded_at_utc");
                ret.Ignore(r => r.ReturnedSubtotal);
                ret.Ignore(r => r.IsOpen);
                ret.Ignore(r => r.HoldsUnits);
                ret.HasIndex(r => r.Status);

                ret.OwnsMany(r => r.Lines, line =>
                {
                    line.ToTable("order_return_lines");
                    line.WithOwner().HasForeignKey("ReturnId");
                    line.Property<Guid>("ReturnId").HasColumnName("return_id");
                    line.HasKey(l => l.Id);
                    line.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
                    line.Property(l => l.VariantId).HasColumnName("variant_id").IsRequired();
                    line.Property(l => l.ProductId).HasColumnName("product_id").IsRequired();
                    line.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
                    line.Property(l => l.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)").IsRequired();
                    line.Property(l => l.Quantity).HasColumnName("quantity").IsRequired();
                });
                ret.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            });
            order.Navigation(o => o.Returns).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
