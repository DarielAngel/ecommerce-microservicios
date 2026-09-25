using Ecommerce.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.ToTable("stock_items");
            entity.HasKey(s => s.VariantId);

            entity.Property(s => s.VariantId).HasColumnName("variant_id").ValueGeneratedNever();
            entity.Property(s => s.QuantityOnHand).HasColumnName("quantity_on_hand").IsRequired();
            entity.Property(s => s.QuantityReserved).HasColumnName("quantity_reserved").IsRequired();
            entity.Property(s => s.LowStockThreshold).HasColumnName("low_stock_threshold").IsRequired();
            entity.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

            entity.Ignore(s => s.QuantityAvailable);
            entity.Ignore(s => s.IsLowStock);
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.ToTable("stock_reservations");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.OrderId).HasColumnName("order_id").IsRequired();
            entity.HasIndex(r => r.OrderId).IsUnique();
            entity.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(r => r.ConfirmedAtUtc).HasColumnName("confirmed_at_utc");
            entity.Property(r => r.ReleasedAtUtc).HasColumnName("released_at_utc");

            entity.OwnsMany(r => r.Lines, line =>
            {
                line.ToTable("stock_reservation_lines");
                line.WithOwner().HasForeignKey(l => l.StockReservationId);
                line.HasKey(l => l.Id);

                line.Property(l => l.VariantId).HasColumnName("variant_id").IsRequired();
                line.Property(l => l.Quantity).HasColumnName("quantity").IsRequired();
            });

            entity.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
