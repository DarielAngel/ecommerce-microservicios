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
        });
    }
}
