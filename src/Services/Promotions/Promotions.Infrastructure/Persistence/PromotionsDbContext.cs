using Ecommerce.Promotions.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Promotions.Infrastructure.Persistence;

public class PromotionsDbContext : DbContext
{
    public PromotionsDbContext(DbContextOptions<PromotionsDbContext> options) : base(options) { }

    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> Redemptions => Set<CouponRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Coupon>(coupon =>
        {
            coupon.ToTable("coupons");
            coupon.HasKey(c => c.Id);

            coupon.Property(c => c.Code).HasColumnName("code").HasMaxLength(Coupon.MaxCodeLength).IsRequired();
            coupon.Property(c => c.Description).HasColumnName("description").HasMaxLength(Coupon.MaxDescriptionLength).IsRequired();
            coupon.Property(c => c.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20).IsRequired();
            coupon.Property(c => c.Value).HasColumnName("value").HasColumnType("numeric(12,2)").IsRequired();
            coupon.Property(c => c.MaxDiscountAmount).HasColumnName("max_discount_amount").HasColumnType("numeric(12,2)");
            coupon.Property(c => c.MinimumSubtotal).HasColumnName("minimum_subtotal").HasColumnType("numeric(12,2)").IsRequired();
            coupon.Property(c => c.StartsAtUtc).HasColumnName("starts_at_utc");
            coupon.Property(c => c.EndsAtUtc).HasColumnName("ends_at_utc");
            coupon.Property(c => c.UsageLimit).HasColumnName("usage_limit");
            coupon.Property(c => c.OncePerCustomer).HasColumnName("once_per_customer").IsRequired();
            coupon.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
            coupon.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            coupon.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

            // El código se guarda ya normalizado en mayúsculas, así que el índice basta para la unicidad.
            coupon.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_coupons_code");
        });

        modelBuilder.Entity<CouponRedemption>(redemption =>
        {
            redemption.ToTable("coupon_redemptions");

            // Una orden = como mucho un canje: reintentar el checkout nunca cuenta dos usos.
            redemption.HasKey(r => r.OrderId);

            redemption.Property(r => r.OrderId).HasColumnName("order_id");
            redemption.Property(r => r.CouponId).HasColumnName("coupon_id").IsRequired();
            redemption.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
            redemption.Property(r => r.Code).HasColumnName("code").HasMaxLength(Coupon.MaxCodeLength).IsRequired();
            redemption.Property(r => r.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(12,2)").IsRequired();
            redemption.Property(r => r.DiscountAmount).HasColumnName("discount_amount").HasColumnType("numeric(12,2)").IsRequired();
            redemption.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            redemption.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            redemption.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

            redemption.HasOne<Coupon>().WithMany().HasForeignKey(r => r.CouponId).OnDelete(DeleteBehavior.Restrict);
            redemption.HasIndex(r => new { r.CouponId, r.Status }).HasDatabaseName("ix_redemptions_coupon_status");
            redemption.HasIndex(r => new { r.CouponId, r.UserId }).HasDatabaseName("ix_redemptions_coupon_user");
        });
    }
}
