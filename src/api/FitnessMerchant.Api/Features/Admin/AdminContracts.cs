using FitnessMerchant.Api.Domain.Catalog;

namespace FitnessMerchant.Api.Features.Admin;

// Inputs are nullable on purpose: a missing field becomes a field error from the validators below,
// not a JSON binding failure, so the form can show it next to the right input.

/// <summary><c>Slug</c> is optional: empty means "generate it from the name".</summary>
public sealed record ProductInput(
    string? Sku,
    string? Name,
    string? Slug,
    string? Description,
    Guid? CategoryId,
    Guid? BrandId,
    decimal? Price,
    int? DiscountPercent,
    int? StockQuantity,
    bool? IsActive,
    Guid? ImageId,
    uint? Version);

public sealed record AdminProductRow(
    Guid Id,
    string Sku,
    string Name,
    string Slug,
    string? ImageUrl,
    string CategoryName,
    string BrandName,
    decimal Price,
    int DiscountPercent,
    decimal SalePrice,
    int StockQuantity,
    bool IsActive,
    DateTimeOffset UpdatedAt);

/// <summary><c>Version</c> goes back with the update; a stale one gets 409 (xmin, D020).</summary>
public sealed record AdminProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string Slug,
    string Description,
    Guid CategoryId,
    Guid BrandId,
    decimal Price,
    int DiscountPercent,
    decimal SalePrice,
    int StockQuantity,
    bool IsActive,
    Guid? ImageId,
    string? ImageUrl,
    bool HasOrders,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public enum DeleteOutcome
{
    Deleted,
    Archived,
}

public sealed record DeleteProductResponse(DeleteOutcome Outcome);

public sealed record CategoryInput(Department? Department, string? Name, string? Slug, string? Description, int? SortOrder);

public sealed record AdminCategoryResponse(
    Guid Id, Department Department, string Name, string Slug, string? Description, int SortOrder, int ProductCount);

public sealed record BrandInput(string? Name, string? Slug, string? Description, string? AccentColor, Guid? LogoImageId);

public sealed record AdminBrandResponse(
    Guid Id, string Name, string Slug, string Description, string AccentColor, Guid? LogoImageId, string? LogoUrl, int ProductCount);

public sealed record AdminImageResponse(Guid Id, string Url, string ContentType, int Width, int Height, int SizeBytes, DateTimeOffset CreatedAt);
