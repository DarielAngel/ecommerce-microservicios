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
    public DbSet<Address> Addresses => Set<Address>();

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

        // Libreta de direcciones (Fase 5). Si cambias algo acá, cambia también el CREATE TABLE
        // de Program.cs: es el que crea la tabla en bases que ya existían antes de esta fase.
        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("user_addresses");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
            entity.HasIndex(a => a.UserId).HasDatabaseName("ix_user_addresses_user_id");
            entity.Property(a => a.Label).HasColumnName("label").HasMaxLength(Address.MaxLabel).IsRequired();
            entity.Property(a => a.RecipientName).HasColumnName("recipient_name").HasMaxLength(Address.MaxName).IsRequired();
            entity.Property(a => a.Phone).HasColumnName("phone").HasMaxLength(Address.MaxPhone);
            entity.Property(a => a.Street).HasColumnName("street").HasMaxLength(Address.MaxLine).IsRequired();
            entity.Property(a => a.Details).HasColumnName("details").HasMaxLength(Address.MaxLine);
            entity.Property(a => a.City).HasColumnName("city").HasMaxLength(Address.MaxCity).IsRequired();
            entity.Property(a => a.Region).HasColumnName("region").HasMaxLength(Address.MaxCity);
            entity.Property(a => a.PostalCode).HasColumnName("postal_code").HasMaxLength(Address.MaxPostalCode);
            entity.Property(a => a.Country).HasColumnName("country").HasMaxLength(Address.MaxCountry).IsRequired();
            entity.Property(a => a.IsDefault).HasColumnName("is_default").IsRequired();
            entity.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .HasConstraintName("fk_user_addresses_users")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
