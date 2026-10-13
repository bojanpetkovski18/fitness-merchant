using FitnessMerchant.Api.Domain.Catalog;

namespace FitnessMerchant.Api.IntegrationTests.Infrastructure;

/// <summary>Minimal valid catalog rows for tests that need their own data.</summary>
internal static class TestCatalog
{
    public static Category Category(string slug = "test-category") => new()
    {
        Id = Guid.CreateVersion7(),
        Department = Department.Supplements,
        Name = $"Category {slug}",
        Slug = slug,
    };

    public static Brand Brand(string slug = "test-brand") => new()
    {
        Id = Guid.CreateVersion7(),
        Name = $"Brand {slug}",
        Slug = slug,
        Description = "A brand used in tests.",
        AccentColor = "#336699",
    };

    public static Product Product(Category category, Brand brand, string slug, string? sku = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Sku = sku ?? $"SKU-{slug}".ToUpperInvariant(),
        Name = $"Product {slug}",
        Slug = slug,
        Description = "A product used in tests.",
        Category = category,
        Brand = brand,
        Price = 10.00m,
        StockQuantity = 5,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
