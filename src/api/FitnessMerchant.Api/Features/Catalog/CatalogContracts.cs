using FitnessMerchant.Api.Domain.Catalog;

namespace FitnessMerchant.Api.Features.Catalog;

/// <summary>Serialized in kebab case: "in-stock", "low-stock", "out-of-stock".</summary>
public enum StockStatus
{
    InStock,
    LowStock,
    OutOfStock,
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record CategoryResponse(string Slug, string Name, string? Description);

public sealed record DepartmentResponse(Department Slug, string Name, IReadOnlyList<CategoryResponse> Categories);

public sealed record BrandResponse(string Slug, string Name, string Description, string AccentColor, string? LogoUrl, int ProductCount);

public sealed record NamedRef(string Slug, string Name);

public sealed record ProductCardResponse(
    Guid Id,
    string Slug,
    string Name,
    NamedRef Brand,
    NamedRef Category,
    Department Department,
    decimal Price,
    int DiscountPercent,
    decimal SalePrice,
    StockStatus StockStatus,
    decimal RatingAverage,
    int RatingCount,
    string? ImageUrl);

/// <param name="MaxOrderQuantity">What the quantity selector allows: stock, capped at 10 per line (SPEC section 5).</param>
public sealed record ProductDetailResponse(
    Guid Id,
    string Slug,
    string Sku,
    string Name,
    string Description,
    NamedRef Brand,
    NamedRef Category,
    Department Department,
    decimal Price,
    int DiscountPercent,
    decimal SalePrice,
    StockStatus StockStatus,
    int MaxOrderQuantity,
    decimal RatingAverage,
    int RatingCount,
    string? ImageUrl);
