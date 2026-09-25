using Ecommerce.Users.Domain.Entities;
using Ecommerce.Users.Domain.Enums;
using Ecommerce.Users.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Users.Infrastructure.Persistence;

public class UsersDbContext : DbContext
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);

            // Email es un Value Object: se mapea como columna simple usando conversión,
            // pero el dominio lo sigue tratando como un tipo con validación propia.
            entity.Property(u => u.Email)
                .HasConversion(email => email.Value, value => Email.Create(value))
                .HasColumnName("email")
                .HasMaxLength(256)
                .IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
            entity.Property(u => u.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.Property(t => t.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
            entity.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(t => t.RevokedAtUtc).HasColumnName("revoked_at_utc");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
