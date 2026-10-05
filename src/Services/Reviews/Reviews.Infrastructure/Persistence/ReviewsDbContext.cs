using Ecommerce.Reviews.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Reviews.Infrastructure.Persistence;

public class ReviewsDbContext : DbContext
{
    public ReviewsDbContext(DbContextOptions<ReviewsDbContext> options) : base(options) { }

    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<VerifiedPurchase> VerifiedPurchases => Set<VerifiedPurchase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Review>(review =>
        {
            review.ToTable("reviews", table =>
                table.HasCheckConstraint("ck_reviews_rating", $"rating >= {Review.MinRating} AND rating <= {Review.MaxRating}"));
            review.HasKey(r => r.Id);

            review.Property(r => r.ProductId).HasColumnName("product_id").IsRequired();
            review.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
            review.Property(r => r.AuthorName).HasColumnName("author_name").HasMaxLength(100).IsRequired();
            review.Property(r => r.Rating).HasColumnName("rating").IsRequired();
            review.Property(r => r.Title).HasColumnName("title").HasMaxLength(Review.MaxTitleLength).IsRequired();
            review.Property(r => r.Comment).HasColumnName("comment").HasMaxLength(Review.MaxCommentLength).IsRequired();
            review.Property(r => r.IsVerifiedPurchase).HasColumnName("is_verified_purchase").IsRequired();
            review.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            review.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

            // Una reseña por usuario y producto: esta restricción es la garantía real (ver CreateReview).
            review.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique().HasDatabaseName("ux_reviews_user_product");
            review.HasIndex(r => new { r.ProductId, r.CreatedAtUtc }).HasDatabaseName("ix_reviews_product_created");
        });

        modelBuilder.Entity<VerifiedPurchase>(purchase =>
        {
            purchase.ToTable("verified_purchases");
            purchase.HasKey(p => new { p.UserId, p.ProductId });

            purchase.Property(p => p.UserId).HasColumnName("user_id");
            purchase.Property(p => p.ProductId).HasColumnName("product_id");
            purchase.Property(p => p.FirstPurchasedAtUtc).HasColumnName("first_purchased_at_utc").IsRequired();
        });
    }
}
