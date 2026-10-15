using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Domain.Reviews;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessMerchant.Api.Infrastructure.Data.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Title).HasMaxLength(Review.TitleMax);
        builder.Property(r => r.Body).HasMaxLength(Review.BodyMax);

        // One review per user and product; also the lookup for "has this user reviewed it?".
        builder.HasIndex(r => new { r.ProductId, r.UserId }).IsUnique();
        // The product page: visible reviews of one product, newest first.
        builder.HasIndex(r => new { r.ProductId, r.IsHidden, r.CreatedAt }).IsDescending(false, false, true);

        // Reviews need an order, and ordered products are archived rather than deleted, so a cascade only
        // fires for test data; it keeps a hard delete from failing on them.
        builder.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_reviews_rating", $"rating BETWEEN {Review.MinRating} AND {Review.MaxRating}");
            t.HasCheckConstraint("ck_reviews_title", $"char_length(title) BETWEEN {Review.TitleMin} AND {Review.TitleMax}");
            t.HasCheckConstraint("ck_reviews_body", $"char_length(body) BETWEEN {Review.BodyMin} AND {Review.BodyMax}");
        });
    }
}
