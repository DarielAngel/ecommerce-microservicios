using Ecommerce.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Notifications.Infrastructure.Persistence;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentNotification>(entity =>
        {
            entity.ToTable("sent_notifications");
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(n => n.ReferenceId).HasColumnName("reference_id").IsRequired();
            entity.Property(n => n.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(256).IsRequired();
            entity.Property(n => n.SentAtUtc).HasColumnName("sent_at_utc").IsRequired();

            // La garantía de idempotencia vive en la base de datos, no solo en el código:
            // aunque dos procesos intenten registrar el mismo envío a la vez, solo uno puede.
            entity.HasIndex(n => new { n.Type, n.ReferenceId }).IsUnique();
        });
    }
}
