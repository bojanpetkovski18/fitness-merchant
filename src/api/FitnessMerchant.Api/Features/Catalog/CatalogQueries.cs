using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Catalog;

/// <summary>Flat row read from SQL; mapped to the response after materialization (image URL, stock status).</summary>
internal sealed record ProductRow(
    Guid Id, string Slug, string Name, string BrandSlug, string BrandName, string CategorySlug, string CategoryName,
    Department Department, decimal Price, int DiscountPercent, decimal SalePrice, int StockQuantity,
    decimal RatingAverage, int RatingCount, Guid? ImageId)
{
    public ProductCardResponse ToCard() => new(
        Id, Slug, Name, new NamedRef(BrandSlug, BrandName), new NamedRef(CategorySlug, CategoryName), Department,
        Price, DiscountPercent, SalePrice, CatalogMapping.Stock(StockQuantity), RatingAverage, RatingCount,
        CatalogMapping.ImageUrl(ImageId));
}

internal static class CatalogMapping
{
    public const int LowStockThreshold = 5;
    public const int MaxOrderQuantity = 10;

    public static StockStatus Stock(int quantity) => quantity switch
    {
        <= 0 => StockStatus.OutOfStock,
        <= LowStockThreshold => StockStatus.LowStock,
        _ => StockStatus.InStock,
    };

    public static string? ImageUrl(Guid? imageId) => imageId is { } id ? $"/api/images/{id}" : null;

    /// <summary>
    /// Sale price in SQL, matching <see cref="Pricing.SalePrice"/>: PostgreSQL's round() on numeric rounds
    /// half away from zero, like MidpointRounding.AwayFromZero (checked by an integration test).
    /// </summary>
    public static IQueryable<ProductRow> ToRows(this IQueryable<Product> products) =>
        products.Select(p => new ProductRow(
            p.Id, p.Slug, p.Name, p.Brand.Slug, p.Brand.Name, p.Category.Slug, p.Category.Name, p.Category.Department,
            p.Price, p.DiscountPercent, Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2), p.StockQuantity,
            p.RatingAverage, p.RatingCount, p.ImageId));
}

public sealed class GetDepartmentsQuery(AppDbContext db)
{
    public async Task<IReadOnlyList<DepartmentResponse>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(c => c.Department).ThenBy(c => c.SortOrder)
            .Select(c => new { c.Department, c.Slug, c.Name, c.Description })
            .ToListAsync(cancellationToken);

        return categories
            .GroupBy(c => c.Department)
            .Select(g => new DepartmentResponse(
                g.Key, g.Key.ToString(), g.Select(c => new CategoryResponse(c.Slug, c.Name, c.Description)).ToList()))
            .ToList();
    }
}

public sealed class ListBrandsQuery(AppDbContext db)
{
    public async Task<IReadOnlyList<BrandResponse>> ExecuteAsync(string? slug, CancellationToken cancellationToken)
    {
        var rows = await db.Brands.AsNoTracking()
            .Where(b => slug == null || b.Slug == slug)
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                b.Slug, b.Name, b.Description, b.AccentColor, b.LogoImageId,
                ProductCount = b.Products.Count(p => p.IsActive),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(b => new BrandResponse(b.Slug, b.Name, b.Description, b.AccentColor, CatalogMapping.ImageUrl(b.LogoImageId), b.ProductCount))
            .ToList();
    }
}

public sealed class ListProductsQuery(AppDbContext db)
{
    /// <summary>Fuzzy (trigram) matching only for terms this long; shorter terms are too noisy (D022).</summary>
    public const int FuzzyMinLength = 4;
    public const double FuzzyThreshold = 0.45;

    public async Task<PagedResponse<ProductCardResponse>> ExecuteAsync(ProductListCriteria criteria, CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (criteria.Department is { } department)
        {
            products = products.Where(p => p.Category.Department == department);
        }
        if (criteria.Categories.Count > 0)
        {
            products = products.Where(p => criteria.Categories.Contains(p.Category.Slug));
        }
        if (criteria.Brands.Count > 0)
        {
            products = products.Where(p => criteria.Brands.Contains(p.Brand.Slug));
        }
        if (criteria.InStockOnly)
        {
            products = products.Where(p => p.StockQuantity > 0);
        }

        var search = criteria.Search;
        if (search is not null)
        {
            // User input is a parameter, and LIKE wildcards in it are escaped, so "100%" means a literal "100%".
            var pattern = $"%{EscapeLike(search)}%";
            var fuzzy = search.Length >= FuzzyMinLength;
            products = products.Where(p =>
                EF.Functions.ILike(p.Name, pattern, @"\") ||
                EF.Functions.ILike(p.Brand.Name, pattern, @"\") ||
                (fuzzy && EF.Functions.TrigramsWordSimilarity(search, p.Name) >= FuzzyThreshold));
        }

        // Filter and sort on the entity; EF cannot translate filters over a constructor-built record,
        // so the projection to rows comes last.
        if (criteria.MinPrice is { } min)
        {
            products = products.Where(p => Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2) >= min);
        }
        if (criteria.MaxPrice is { } max)
        {
            products = products.Where(p => Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2) <= max);
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var page = await Sort(products, criteria.Sort, search)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToRows()
            .ToListAsync(cancellationToken);

        return new PagedResponse<ProductCardResponse>(
            page.Select(r => r.ToCard()).ToList(), criteria.Page, criteria.PageSize, totalCount);
    }

    /// <summary>Every sort ends with the ID so paging is stable across requests.</summary>
    private static IQueryable<Product> Sort(IQueryable<Product> products, ProductSort sort, string? search) =>
        sort switch
        {
            ProductSort.Relevance when search is not null => products
                .OrderByDescending(p => EF.Functions.TrigramsWordSimilarity(search, p.Name))
                .ThenBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.PriceAsc => products
                .OrderBy(p => Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2)).ThenBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.PriceDesc => products
                .OrderByDescending(p => Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2)).ThenBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.Rating => products
                .OrderByDescending(p => p.RatingAverage).ThenByDescending(p => p.RatingCount).ThenBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.Discount => products
                .OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.Price).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
        };

    internal static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}

public sealed class GetProductQuery(AppDbContext db)
{
    public async Task<ProductDetailResponse?> ExecuteAsync(string slug, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Slug == slug)
            .Select(p => new
            {
                p.Id, p.Slug, p.Sku, p.Name, p.Description,
                Brand = new NamedRef(p.Brand.Slug, p.Brand.Name),
                Category = new NamedRef(p.Category.Slug, p.Category.Name),
                p.Category.Department, p.Price, p.DiscountPercent,
                SalePrice = Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2),
                p.StockQuantity, p.RatingAverage, p.RatingCount, p.ImageId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return product is null
            ? null
            : new ProductDetailResponse(
                product.Id, product.Slug, product.Sku, product.Name, product.Description, product.Brand, product.Category,
                product.Department, product.Price, product.DiscountPercent, product.SalePrice,
                CatalogMapping.Stock(product.StockQuantity), Math.Clamp(product.StockQuantity, 0, CatalogMapping.MaxOrderQuantity),
                product.RatingAverage, product.RatingCount, CatalogMapping.ImageUrl(product.ImageId));
    }
}

public sealed class GetRelatedProductsQuery(AppDbContext db)
{
    public const int Count = 4;

    /// <summary>Same category first, then same brand; null when the product does not exist.</summary>
    public async Task<IReadOnlyList<ProductCardResponse>?> ExecuteAsync(string slug, CancellationToken cancellationToken)
    {
        var source = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Slug == slug)
            .Select(p => new { p.Id, p.CategoryId, p.BrandId })
            .SingleOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            return null;
        }

        var related = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Id != source.Id && (p.CategoryId == source.CategoryId || p.BrandId == source.BrandId))
            .OrderBy(p => p.CategoryId == source.CategoryId ? 0 : 1)
            .ThenByDescending(p => p.StockQuantity > 0)
            .ThenByDescending(p => p.RatingAverage)
            .ThenByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(Count)
            .ToRows()
            .ToListAsync(cancellationToken);

        return related.Select(r => r.ToCard()).ToList();
    }
}
