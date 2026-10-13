using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessMerchant.Api.Infrastructure.Data.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Department).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Name).HasMaxLength(80);
        builder.Property(c => c.Slug).HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(500);

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => new { c.Department, c.SortOrder });
    }
}

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Name).HasMaxLength(80);
        builder.Property(b => b.Slug).HasMaxLength(100);
        builder.Property(b => b.Description).HasMaxLength(1000);
        builder.Property(b => b.AccentColor).HasMaxLength(7).IsFixedLength();

        builder.HasIndex(b => b.Name).IsUnique();
        builder.HasIndex(b => b.Slug).IsUnique();

        builder.HasOne(b => b.LogoImage).WithMany().HasForeignKey(b => b.LogoImageId).OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t => t.HasCheckConstraint("ck_brands_accent_color", "accent_color ~ '^#[0-9a-f]{6}$'"));
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Sku).HasMaxLength(32);
        builder.Property(p => p.Name).HasMaxLength(120);
        builder.Property(p => p.Slug).HasMaxLength(140);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.RatingAverage).HasPrecision(3, 2);
        builder.Property(p => p.Version).IsRowVersion();
        builder.Ignore(p => p.SalePrice);

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.BrandId);
        builder.HasIndex(p => new { p.IsActive, p.CreatedAt });
        builder.HasIndex(p => p.Name).HasMethod("gin").HasOperators("gin_trgm_ops");

        builder.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Brand).WithMany(b => b.Products).HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Image).WithMany().HasForeignKey(p => p.ImageId).OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_products_price", "price >= 0");
            t.HasCheckConstraint("ck_products_discount_percent", $"discount_percent BETWEEN 0 AND {Pricing.MaxDiscountPercent}");
            t.HasCheckConstraint("ck_products_stock_quantity", "stock_quantity >= 0");
            t.HasCheckConstraint("ck_products_rating", "rating_average BETWEEN 0 AND 5 AND rating_count >= 0");
        });
    }
}

internal sealed class ImageConfiguration : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> builder)
    {
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Sha256).HasMaxLength(64).IsFixedLength();
        builder.Property(i => i.ContentType).HasMaxLength(50);

        builder.HasIndex(i => i.Sha256).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_images_size", "size_bytes > 0 AND width > 0 AND height > 0"));
    }
}
