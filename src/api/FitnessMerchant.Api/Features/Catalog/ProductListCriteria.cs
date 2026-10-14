using System.Globalization;
using System.Text.RegularExpressions;
using FitnessMerchant.Api.Domain.Catalog;

namespace FitnessMerchant.Api.Features.Catalog;

public enum ProductSort
{
    Relevance,
    Newest,
    PriceAsc,
    PriceDesc,
    Rating,
    Discount,
}

/// <summary>Raw query string of <c>GET /api/catalog/products</c>; everything is a string so errors can be explained.</summary>
public sealed class ProductListParameters
{
    public string? Q { get; init; }
    public string? Department { get; init; }
    public string[]? Category { get; init; }
    public string[]? Brand { get; init; }
    public string? MinPrice { get; init; }
    public string? MaxPrice { get; init; }
    public string? InStock { get; init; }
    public string? Sort { get; init; }
    public string? Page { get; init; }
    public string? PageSize { get; init; }
}

/// <summary>Validated product-list criteria. Sort keys come from a fixed allow-list, never from raw input (CLAUDE.md).</summary>
public sealed partial record ProductListCriteria(
    string? Search,
    Department? Department,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Brands,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool InStockOnly,
    ProductSort Sort,
    int Page,
    int PageSize)
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 48;
    public const int MaxSearchLength = 100;
    private const int MaxSlugsPerFilter = 20;
    private const decimal MaxPriceValue = 1_000_000m;

    public static readonly IReadOnlyDictionary<string, ProductSort> SortKeys = new Dictionary<string, ProductSort>(StringComparer.Ordinal)
    {
        ["relevance"] = ProductSort.Relevance,
        ["newest"] = ProductSort.Newest,
        ["price-asc"] = ProductSort.PriceAsc,
        ["price-desc"] = ProductSort.PriceDesc,
        ["rating"] = ProductSort.Rating,
        ["discount"] = ProductSort.Discount,
    };

    public static (ProductListCriteria? Criteria, Dictionary<string, string[]> Errors) Parse(ProductListParameters input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Fail(string field, string message) => errors[field] = [message];

        var search = string.IsNullOrWhiteSpace(input.Q) ? null : input.Q.Trim();
        if (search is { Length: > MaxSearchLength })
        {
            Fail("q", $"Search must be at most {MaxSearchLength} characters.");
        }

        Department? department = null;
        if (!string.IsNullOrEmpty(input.Department))
        {
            if (Enum.TryParse<Department>(input.Department, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                && !input.Department.All(char.IsAsciiDigit))
            {
                department = parsed;
            }
            else
            {
                Fail("department", "Department must be 'supplements' or 'gear'.");
            }
        }

        var categories = Slugs(input.Category, "category", Fail);
        var brands = Slugs(input.Brand, "brand", Fail);
        var minPrice = Price(input.MinPrice, "minPrice", Fail);
        var maxPrice = Price(input.MaxPrice, "maxPrice", Fail);
        if (minPrice > maxPrice)
        {
            Fail("maxPrice", "Maximum price must not be lower than the minimum price.");
        }

        var inStockOnly = false;
        if (!string.IsNullOrEmpty(input.InStock) && !bool.TryParse(input.InStock, out inStockOnly))
        {
            Fail("inStock", "inStock must be 'true' or 'false'.");
        }

        var sort = search is null ? ProductSort.Newest : ProductSort.Relevance;
        if (!string.IsNullOrEmpty(input.Sort))
        {
            if (SortKeys.TryGetValue(input.Sort, out var parsedSort))
            {
                // Relevance needs a search term; without one it means "newest".
                sort = parsedSort == ProductSort.Relevance && search is null ? ProductSort.Newest : parsedSort;
            }
            else
            {
                Fail("sort", $"Sort must be one of: {string.Join(", ", SortKeys.Keys)}.");
            }
        }

        var page = Integer(input.Page, "page", 1, 1, 10_000, Fail);
        var pageSize = Integer(input.PageSize, "pageSize", DefaultPageSize, 1, MaxPageSize, Fail);

        return errors.Count > 0
            ? (null, errors)
            : (new ProductListCriteria(search, department, categories, brands, minPrice, maxPrice, inStockOnly, sort, page, pageSize), errors);
    }

    private static string[] Slugs(string[]? values, string field, Action<string, string> fail)
    {
        var slugs = (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (slugs.Length > MaxSlugsPerFilter)
        {
            fail(field, $"At most {MaxSlugsPerFilter} values are allowed.");
        }
        else if (slugs.Any(s => !SlugPattern().IsMatch(s)))
        {
            fail(field, "Values must be slugs (lowercase letters, digits and hyphens).");
        }
        return slugs;
    }

    private static decimal? Price(string? raw, string field, Action<string, string> fail)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }
        if (decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value <= MaxPriceValue)
        {
            return value;
        }
        fail(field, $"Price must be a number between 0 and {MaxPriceValue.ToString(CultureInfo.InvariantCulture)}.");
        return null;
    }

    private static int Integer(string? raw, string field, int fallback, int min, int max, Action<string, string> fail)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return fallback;
        }
        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
        {
            return value;
        }
        fail(field, $"{field} must be a whole number from {min} to {max}.");
        return fallback;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
