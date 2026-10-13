using System.Security.Cryptography;
using System.Text;
using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Domain.Media;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Infrastructure.Seed;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    public bool Enabled { get; init; }
}

/// <summary>
/// Fills an empty catalog with <see cref="CatalogSeedData"/> in one transaction. A catalog that already has
/// categories is left alone, so running it again is a no-op; the demo reset (M12) empties the catalog first.
/// IDs are deterministic (<see cref="SeedIds"/>); timestamps are relative to now so "new arrivals" stays fresh.
/// </summary>
public sealed partial class CatalogSeeder(AppDbContext db, TimeProvider time, ILogger<CatalogSeeder> logger)
{
    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Categories.AnyAsync(cancellationToken))
        {
            LogSkipped(logger);
            return false;
        }

        var now = time.GetUtcNow();
        var images = new Dictionary<string, Image>(StringComparer.Ordinal);

        var categories = CatalogSeedData.Categories.ToDictionary(c => c.Slug, c => new Category
        {
            Id = SeedIds.For($"category:{c.Slug}"),
            Department = c.Department,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            SortOrder = c.SortOrder,
        });

        var brandSeeds = CatalogSeedData.Brands.ToDictionary(b => b.Slug);
        var brands = CatalogSeedData.Brands.ToDictionary(b => b.Slug, b => new Brand
        {
            Id = SeedIds.For($"brand:{b.Slug}"),
            Name = b.Name,
            Slug = b.Slug,
            Description = b.Description,
            AccentColor = b.AccentColor,
            LogoImage = SvgImage(images, Artwork.BrandLogo(b.Name, b.AccentColor), 600, 200, now),
        });

        var skuCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        var products = CatalogSeedData.Products.Select(p =>
        {
            var brandSeed = brandSeeds[p.Brand];
            var slug = Slugs.Slugify(p.Name);
            var sequence = skuCounters[brandSeed.SkuPrefix] = skuCounters.GetValueOrDefault(brandSeed.SkuPrefix) + 1;
            var createdAt = now.AddDays(-p.AddedDaysAgo);
            return new Product
            {
                Id = SeedIds.For($"product:{slug}"),
                Sku = $"{brandSeed.SkuPrefix}-{sequence:D4}",
                Name = p.Name,
                Slug = slug,
                Description = p.Description,
                Category = categories[p.Category],
                Brand = brands[p.Brand],
                Price = p.Price,
                DiscountPercent = p.Discount,
                StockQuantity = p.Stock,
                Image = SvgImage(images, Artwork.Product(p.Art, brandSeed.Name, brandSeed.AccentColor), Artwork.Size, Artwork.Size, now),
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };
        }).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Products.AddRange(products); // categories, brands and images are added through the navigations
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogSeeded(logger, categories.Count, brands.Count, products.Count, images.Count);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog already has data; seeding skipped.")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded catalog: {Categories} categories, {Brands} brands, {Products} products, {Images} images.")]
    private static partial void LogSeeded(ILogger logger, int categories, int brands, int products, int images);

    /// <summary>Content-addressed: identical artwork is stored once and shared.</summary>
    private static Image SvgImage(Dictionary<string, Image> images, string svg, int width, int height, DateTimeOffset now)
    {
        var data = Encoding.UTF8.GetBytes(svg);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(data));
        if (!images.TryGetValue(sha256, out var image))
        {
            image = new Image
            {
                Id = SeedIds.For($"image:{sha256}"),
                Sha256 = sha256,
                ContentType = "image/svg+xml",
                Data = data,
                SizeBytes = data.Length,
                Width = width,
                Height = height,
                CreatedAt = now,
            };
            images.Add(sha256, image);
        }
        return image;
    }
}
