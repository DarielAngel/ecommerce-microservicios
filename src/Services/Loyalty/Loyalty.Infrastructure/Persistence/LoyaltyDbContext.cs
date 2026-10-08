using Ecommerce.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Loyalty.Infrastructure.Persistence;

public class LoyaltyDbContext : DbContext
{
    public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options) : base(options) { }

    public DbSet<LoyaltyAccount> Accounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyEntry> Entries => Set<LoyaltyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoyaltyAccount>(account =>
        {
            account.ToTable("loyalty_accounts");
            account.HasKey(a => a.UserId);
            account.Property(a => a.UserId).HasColumnName("user_id").ValueGeneratedNever();
            account.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        });

        modelBuilder.Entity<LoyaltyEntry>(entry =>
        {
            entry.ToTable("loyalty_entries");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entry.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entry.HasIndex(e => e.UserId).HasDatabaseName("ix_loyalty_entries_user_id");
            entry.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
            entry.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20).IsRequired();
            entry.Property(e => e.ReferenceId).HasColumnName("reference_id").IsRequired();
            // Una orden gana puntos una sola vez y canjea una sola vez; y cada devolución ajusta una sola vez.
            entry.HasIndex(e => new { e.OrderId, e.Kind, e.ReferenceId }).IsUnique().HasDatabaseName("ux_loyalty_entries_order_kind_ref");
            entry.Ignore(e => e.Adds);
            entry.Property(e => e.Points).HasColumnName("points").IsRequired();
            entry.Property(e => e.DiscountAmount).HasColumnName("discount_amount").HasColumnType("numeric(12,2)").IsRequired();
            entry.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entry.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entry.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entry.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        });
    }
}
