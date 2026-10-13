using FitnessMerchant.Api.Domain.Media;

namespace FitnessMerchant.Api.Domain.Catalog;

public sealed class Product
{
    public Guid Id { get; init; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public decimal Price { get; set; }
    public int DiscountPercent { get; set; }
    public int StockQuantity { get; set; }

    /// <summary>Archived products (false) stay in the database for existing orders but leave the storefront.</summary>
    public bool IsActive { get; set; } = true;

    public Guid? ImageId { get; set; }
    public Image? Image { get; set; }

    /// <summary>Maintained from visible reviews (M11); 0 with no reviews.</summary>
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token, mapped to PostgreSQL's <c>xmin</c> system column.</summary>
    public uint Version { get; set; }

    public decimal SalePrice => Pricing.SalePrice(Price, DiscountPercent);
}
