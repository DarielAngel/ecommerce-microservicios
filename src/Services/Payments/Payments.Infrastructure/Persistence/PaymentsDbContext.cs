using Ecommerce.Payments.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Payments.Infrastructure.Persistence;

public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.OrderId).HasColumnName("order_id").IsRequired();
            entity.HasIndex(p => p.OrderId).IsUnique(); // un solo pago activo por orden

            entity.Property(p => p.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            entity.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.Property(p => p.PayPalOrderId).HasColumnName("paypal_order_id").HasMaxLength(64).IsRequired();
            entity.HasIndex(p => p.PayPalOrderId).IsUnique();

            entity.Property(p => p.PayPalCaptureId).HasColumnName("paypal_capture_id").HasMaxLength(64);
            entity.Property(p => p.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);

            entity.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(p => p.CapturedAtUtc).HasColumnName("captured_at_utc");
            entity.Ignore(p => p.RefundedAmount);
            entity.Ignore(p => p.RefundableAmount);

            // Fase 7: reembolsos (pueden ser varios parciales). El Id lo elige Órdenes (id de la devolución).
            entity.OwnsMany(p => p.Refunds, refund =>
            {
                refund.ToTable("payment_refunds");
                refund.WithOwner().HasForeignKey("PaymentId");
                refund.Property<Guid>("PaymentId").HasColumnName("payment_id");
                refund.HasKey(r => r.Id);
                refund.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
                refund.Property(r => r.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)").IsRequired();
                refund.Property(r => r.PayPalRefundId).HasColumnName("paypal_refund_id").HasMaxLength(64).IsRequired();
                refund.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(255);
                refund.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            });
            entity.Navigation(p => p.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
