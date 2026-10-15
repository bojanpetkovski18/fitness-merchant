using System.Text.RegularExpressions;
using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Domain.Catalog;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>Field errors keyed like the form fields (camelCase), in the order they were found.</summary>
internal sealed class FieldErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            _errors[field] = messages = [];
        }
        messages.Add(message);
    }

    public Dictionary<string, string[]> ToDictionary() => _errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
}

internal sealed record ValidProduct(
    string Sku, string Name, string? Slug, string Description, Guid CategoryId, Guid BrandId,
    decimal Price, int DiscountPercent, int StockQuantity, bool IsActive, Guid? ImageId);

internal sealed record ValidCategory(Department Department, string Name, string? Slug, string? Description, int SortOrder);

internal sealed record ValidBrand(string Name, string? Slug, string Description, string AccentColor, Guid? LogoImageId);

/// <summary>
/// Shape rules for admin catalog input. Rules that need the database (unique SKU and slug, existing category,
/// brand and image) are checked by the handlers, with the unique indexes as the final word.
/// </summary>
internal static partial class AdminValidation
{
    public const decimal MaxPrice = 99_999.99m;
    public const int MaxStock = 100_000;
    public const int MaxSortOrder = 1_000;

    public static (ValidProduct? Product, FieldErrors Errors) Product(ProductInput input)
    {
        var errors = new FieldErrors();

        var sku = input.Sku?.Trim().ToUpperInvariant() ?? "";
        if (sku.Length == 0)
        {
            errors.Add("sku", "SKU is required.");
        }
        else if (sku.Length > 32 || !SkuPattern().IsMatch(sku))
        {
            errors.Add("sku", "Use up to 32 letters, digits and single hyphens, e.g. HAL-0001.");
        }

        var name = Text(input.Name, "name", "Name", 2, 120, errors);
        var slug = OptionalSlug(input.Slug, 140, errors);
        var description = Text(input.Description, "description", "Description", 1, 2000, errors);

        if (input.CategoryId is null)
        {
            errors.Add("categoryId", "Choose a category.");
        }
        if (input.BrandId is null)
        {
            errors.Add("brandId", "Choose a brand.");
        }

        switch (input.Price)
        {
            case null:
                errors.Add("price", "Price is required.");
                break;
            case decimal price when price < 0.01m || price > MaxPrice:
                errors.Add("price", $"Price must be between 0.01 and {MaxPrice:0.00}.");
                break;
            case decimal price when decimal.Round(price, 2) != price:
                errors.Add("price", "Price can have at most 2 decimal places.");
                break;
        }

        if (input.DiscountPercent is < 0 or > Pricing.MaxDiscountPercent)
        {
            errors.Add("discountPercent", $"Discount must be between 0 and {Pricing.MaxDiscountPercent} %.");
        }
        if (input.StockQuantity is null)
        {
            errors.Add("stockQuantity", "Stock is required.");
        }
        else if (input.StockQuantity is < 0 or > MaxStock)
        {
            errors.Add("stockQuantity", $"Stock must be between 0 and {MaxStock}.");
        }

        return errors.Any
            ? (null, errors)
            : (new ValidProduct(sku, name!, slug, description!, input.CategoryId!.Value, input.BrandId!.Value, input.Price!.Value,
                input.DiscountPercent ?? 0, input.StockQuantity!.Value, input.IsActive ?? true, input.ImageId), errors);
    }

    public static (ValidCategory? Category, FieldErrors Errors) Category(CategoryInput input)
    {
        var errors = new FieldErrors();
        if (input.Department is null)
        {
            errors.Add("department", "Choose a department.");
        }
        var name = Text(input.Name, "name", "Name", 2, 80, errors);
        var slug = OptionalSlug(input.Slug, 100, errors);
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : Text(input.Description, "description", "Description", 1, 500, errors);
        if (input.SortOrder is < 0 or > MaxSortOrder)
        {
            errors.Add("sortOrder", $"Sort order must be between 0 and {MaxSortOrder}.");
        }

        return errors.Any
            ? (null, errors)
            : (new ValidCategory(input.Department!.Value, name!, slug, description, input.SortOrder ?? 0), errors);
    }

    public static (ValidBrand? Brand, FieldErrors Errors) Brand(BrandInput input)
    {
        var errors = new FieldErrors();
        var name = Text(input.Name, "name", "Name", 2, 80, errors);
        var slug = OptionalSlug(input.Slug, 100, errors);
        var description = Text(input.Description, "description", "Description", 1, 1000, errors);
        var accent = input.AccentColor?.Trim().ToLowerInvariant() ?? "";
        if (!AccentPattern().IsMatch(accent))
        {
            errors.Add("accentColor", "Use a colour like #1f6feb.");
        }

        return errors.Any
            ? (null, errors)
            : (new ValidBrand(name!, slug, description!, accent, input.LogoImageId), errors);
    }

    private static string? Text(string? value, string field, string label, int min, int max, FieldErrors errors)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
        {
            errors.Add(field, $"{label} is required.");
            return null;
        }
        if (text.Length < min || text.Length > max)
        {
            errors.Add(field, $"{label} must be {min} to {max} characters.");
            return null;
        }
        return text;
    }

    /// <summary>Null when empty (generate one from the name); otherwise it must already be slug-shaped.</summary>
    private static string? OptionalSlug(string? value, int max, FieldErrors errors)
    {
        var slug = value?.Trim() ?? "";
        if (slug.Length == 0)
        {
            return null;
        }
        if (slug.Length > max || Slugs.Slugify(slug) != slug)
        {
            var example = Slugs.Slugify(slug) is { Length: > 0 } suggestion ? suggestion : "my-product";
            errors.Add("slug", $"Use up to {max} lowercase letters, digits and single hyphens, e.g. {example}.");
            return null;
        }
        return slug;
    }

    [GeneratedRegex("^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial Regex SkuPattern();

    [GeneratedRegex("^#[0-9a-f]{6}$")]
    private static partial Regex AccentPattern();
}

public enum AdminProductSort
{
    UpdatedDesc,
    UpdatedAsc,
    NameAsc,
    NameDesc,
    SkuAsc,
    SkuDesc,
    PriceAsc,
    PriceDesc,
    StockAsc,
    StockDesc,
}

public enum AdminProductStatus
{
    All,
    Active,
    Archived,
}

/// <summary>Raw query string of <c>GET /api/admin/products</c>.</summary>
public sealed class AdminProductListParameters
{
    public string? Q { get; init; }
    public Guid? Category { get; init; }
    public Guid? Brand { get; init; }
    public string? Status { get; init; }
    public string? Sort { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

internal sealed record AdminProductCriteria(
    string? Search, Guid? Category, Guid? Brand, AdminProductStatus Status, AdminProductSort Sort, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 48;

    /// <summary>The only sort keys accepted; never a column name from input (CLAUDE.md).</summary>
    public static readonly IReadOnlyDictionary<string, AdminProductSort> SortKeys = new Dictionary<string, AdminProductSort>(StringComparer.Ordinal)
    {
        ["updated-desc"] = AdminProductSort.UpdatedDesc,
        ["updated-asc"] = AdminProductSort.UpdatedAsc,
        ["name-asc"] = AdminProductSort.NameAsc,
        ["name-desc"] = AdminProductSort.NameDesc,
        ["sku-asc"] = AdminProductSort.SkuAsc,
        ["sku-desc"] = AdminProductSort.SkuDesc,
        ["price-asc"] = AdminProductSort.PriceAsc,
        ["price-desc"] = AdminProductSort.PriceDesc,
        ["stock-asc"] = AdminProductSort.StockAsc,
        ["stock-desc"] = AdminProductSort.StockDesc,
    };

    public static readonly IReadOnlyDictionary<string, AdminProductStatus> StatusKeys = new Dictionary<string, AdminProductStatus>(StringComparer.Ordinal)
    {
        ["all"] = AdminProductStatus.All,
        ["active"] = AdminProductStatus.Active,
        ["archived"] = AdminProductStatus.Archived,
    };

    public static (AdminProductCriteria? Criteria, FieldErrors Errors) From(AdminProductListParameters p)
    {
        var errors = new FieldErrors();
        var search = p.Q?.Trim() is { Length: > 0 } q ? q : null;
        if (search is { Length: > 100 })
        {
            errors.Add("q", "Search is limited to 100 characters.");
        }

        var sort = AdminProductSort.UpdatedDesc;
        if (p.Sort is { Length: > 0 } && !SortKeys.TryGetValue(p.Sort, out sort))
        {
            errors.Add("sort", $"Sort must be one of: {string.Join(", ", SortKeys.Keys)}.");
        }

        var status = AdminProductStatus.All;
        if (p.Status is { Length: > 0 } && !StatusKeys.TryGetValue(p.Status, out status))
        {
            errors.Add("status", "Status must be all, active or archived.");
        }

        var page = p.Page ?? 1;
        if (page is < 1 or > 10_000)
        {
            errors.Add("page", "Page must be between 1 and 10000.");
        }
        var pageSize = p.PageSize ?? DefaultPageSize;
        if (pageSize is < 1 or > MaxPageSize)
        {
            errors.Add("pageSize", $"Page size must be between 1 and {MaxPageSize}.");
        }

        return errors.Any ? (null, errors) : (new AdminProductCriteria(search, p.Category, p.Brand, status, sort, page, pageSize), errors);
    }
}
