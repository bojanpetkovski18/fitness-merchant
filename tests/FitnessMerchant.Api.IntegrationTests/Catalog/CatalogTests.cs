using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.IntegrationTests.Catalog;

/// <summary>
/// Every list response is checked against an oracle: all products loaded into memory and filtered/sorted in C#
/// with the domain pricing rule, so the SQL translation is verified, not just the shape of the response.
/// </summary>
public class CatalogTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record OracleProduct(
        Guid Id, string Slug, string Name, string CategorySlug, string BrandSlug, string BrandName, Department Department,
        decimal Price, int Discount, decimal SalePrice, int Stock, DateTimeOffset CreatedAt, bool IsActive);

    [Fact]
    public async Task Departments_list_both_departments_with_their_categories_in_order()
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var departments = await client.GetFromJsonAsync<List<DepartmentResponse>>("/api/catalog/departments", Json, Ct);

        Assert.Equal([Department.Supplements, Department.Gear], departments!.Select(d => d.Slug).OrderBy(d => d).ToArray());
        var supplements = departments!.Single(d => d.Slug == Department.Supplements);
        Assert.Equal(["protein", "pre-workout", "creatine-amino-acids", "vitamins-health"], supplements.Categories.Select(c => c.Slug));
    }

    [Fact]
    public async Task Brands_have_logos_and_active_product_counts()
    {
        await using var api = await NewApiAsync();
        var (client, oracle) = (api.Client, api.Oracle);

        var brands = await client.GetFromJsonAsync<List<BrandResponse>>("/api/catalog/brands", Json, Ct);
        var halvard = await client.GetFromJsonAsync<BrandResponse>("/api/catalog/brands/halvard-nutrition", Json, Ct);

        Assert.Equal(6, brands!.Count);
        Assert.Equal(oracle.Count(p => p.IsActive), brands.Sum(b => b.ProductCount));
        Assert.All(brands, b => Assert.StartsWith("/api/images/", b.LogoUrl, StringComparison.Ordinal));
        Assert.Equal(oracle.Count(p => p.BrandSlug == "halvard-nutrition"), halvard!.ProductCount);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/catalog/brands/no-such-brand", Ct)).StatusCode);
    }

    public static TheoryData<string> FilterQueries => new()
    {
        "",
        "department=gear",
        "department=supplements",
        "category=protein",
        "category=protein&category=pre-workout",
        "brand=verdance-fuel",
        "brand=verdance-fuel&brand=orrin-cardio",
        "minPrice=50",
        "maxPrice=19.90",
        "minPrice=20&maxPrice=60",
        "inStock=true",
        "department=supplements&category=protein&category=creatine-amino-acids&brand=halvard-nutrition&minPrice=20&maxPrice=60&inStock=true",
        "department=gear&category=protein", // valid but empty
    };

    [Theory]
    [MemberData(nameof(FilterQueries))]
    public async Task Filters_return_exactly_the_matching_active_products(string query)
    {
        await using var api = await NewApiAsync();
        var (client, oracle) = (api.Client, api.Oracle);

        var first = await ListAsync(client, $"{query}&pageSize=48");
        var second = await ListAsync(client, $"{query}&pageSize=48&page=2");

        var expected = ApplyFilters(oracle, query);
        Assert.Equal(expected.Count, first.TotalCount);
        Assert.Equal(
            expected.Select(p => p.Slug).Order(StringComparer.Ordinal),
            first.Items.Concat(second.Items).Select(i => i.Slug).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("newest")]
    [InlineData("price-asc")]
    [InlineData("price-desc")]
    [InlineData("rating")]
    [InlineData("discount")]
    public async Task Each_sort_orders_the_whole_catalog_across_pages(string sort)
    {
        await using var api = await NewApiAsync();
        var (client, oracle) = (api.Client, api.Oracle);

        var first = await ListAsync(client, $"sort={sort}&pageSize=40&page=1");
        var second = await ListAsync(client, $"sort={sort}&pageSize=40&page=2");
        var actual = first.Items.Concat(second.Items).Select(i => i.Slug).ToList();

        // Ties are broken in SQL (collation, uuid order), so check the sort key is monotonic rather than
        // comparing exact sequences with C#'s different string/Guid ordering.
        var products = actual.Select(slug => oracle.Single(p => p.Slug == slug)).ToList();
        Assert.Equal(oracle.Count(p => p.IsActive), actual.Count);
        Assert.Equal(actual.Count, actual.Distinct().Count()); // nothing repeated or skipped across pages
        switch (sort)
        {
            case "newest":
                AssertMonotonic(products.Select(p => p.CreatedAt), descending: true);
                break;
            case "price-asc":
                AssertMonotonic(products.Select(p => p.SalePrice), descending: false);
                break;
            case "price-desc":
                AssertMonotonic(products.Select(p => p.SalePrice), descending: true);
                break;
            case "discount":
                AssertMonotonic(products.Select(p => p.Discount), descending: true);
                break;
            default:
                // rating: every product has 0 until reviews exist (M11); the order must still be complete and stable
                break;
        }
    }

    private static void AssertMonotonic<T>(IEnumerable<T> values, bool descending)
        where T : IComparable<T>
    {
        var list = values.ToList();
        for (var i = 1; i < list.Count; i++)
        {
            var comparison = list[i - 1].CompareTo(list[i]);
            Assert.True(descending ? comparison >= 0 : comparison <= 0, $"Out of order at position {i}: {list[i - 1]} then {list[i]}");
        }
    }

    [Fact]
    public async Task Sale_prices_and_stock_statuses_match_the_domain_rules()
    {
        await using var api = await NewApiAsync();
        var (client, oracle) = (api.Client, api.Oracle);

        var items = (await ListAsync(client, "pageSize=48")).Items.Concat((await ListAsync(client, "pageSize=48&page=2")).Items);

        Assert.All(items, item =>
        {
            Assert.Equal(Pricing.SalePrice(item.Price, item.DiscountPercent), item.SalePrice);
            var stock = oracle.Single(p => p.Id == item.Id).Stock;
            var expected = stock == 0 ? StockStatus.OutOfStock : stock <= 5 ? StockStatus.LowStock : StockStatus.InStock;
            Assert.Equal(expected, item.StockStatus);
            Assert.StartsWith("/api/images/", item.ImageUrl, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task A_page_beyond_the_end_is_empty_but_reports_the_total()
    {
        await using var api = await NewApiAsync();
        var (client, oracle) = (api.Client, api.Oracle);

        var page = await ListAsync(client, "page=99&pageSize=24");

        Assert.Empty(page.Items);
        Assert.Equal(oracle.Count(p => p.IsActive), page.TotalCount);
        Assert.Equal(99, page.Page);
    }

    [Theory]
    [InlineData("sort=name", "sort")]
    [InlineData("sort=price;drop table products", "sort")]
    [InlineData("pageSize=49", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("page=abc", "page")]
    [InlineData("minPrice=-1", "minPrice")]
    [InlineData("minPrice=50&maxPrice=10", "maxPrice")]
    [InlineData("department=shoes", "department")]
    [InlineData("category=Not%20A%20Slug", "category")]
    [InlineData("inStock=maybe", "inStock")]
    public async Task Invalid_parameters_return_400_problem_details_naming_the_field(string query, string field)
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var response = await client.GetAsync($"/api/catalog/products?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct);
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("creatin", "creatine-monohydrate-500-g")] // partial word
    [InlineData("protien", "casein-night-protein-1-kg")] // misspelled
    [InlineData("kettlebel", "competition-kettlebell-16-kg")]
    [InlineData("CHALK", "chalk-block-56-g")] // case-insensitive
    [InlineData("verdance", "greens-superfood-powder-300-g")] // brand name
    public async Task Search_finds_partial_misspelled_and_brand_matches(string q, string expectedSlug)
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var page = await ListAsync(client, $"q={Uri.EscapeDataString(q)}&pageSize=48");

        Assert.Contains(expectedSlug, page.Items.Select(i => i.Slug));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("xyzzy")]
    public async Task Search_treats_wildcards_literally_and_returns_nothing_for_nonsense(string q)
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var page = await ListAsync(client, $"q={Uri.EscapeDataString(q)}");

        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Search_ranks_the_closest_name_first()
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var page = await ListAsync(client, "q=kettlebel");

        Assert.All(page.Items.Take(2), i => Assert.StartsWith("competition-kettlebell", i.Slug, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Product_detail_and_related_products()
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var product = await client.GetFromJsonAsync<ProductDetailResponse>("/api/catalog/products/whey-isolate-vanilla-2-kg", Json, Ct);
        var related = await client.GetFromJsonAsync<List<ProductCardResponse>>("/api/catalog/products/whey-isolate-vanilla-2-kg/related", Json, Ct);

        Assert.Equal("HAL-0001", product!.Sku);
        Assert.Equal(53.91m, product.SalePrice);
        Assert.Equal(10, product.MaxOrderQuantity); // 42 in stock, capped at 10 per line
        Assert.Equal(4, related!.Count);
        Assert.DoesNotContain(related, r => r.Slug == product.Slug);
        Assert.All(related, r => Assert.Equal("protein", r.Category.Slug)); // the category has enough products
    }

    [Fact]
    public async Task Low_stock_products_cap_the_order_quantity_at_stock()
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var product = await client.GetFromJsonAsync<ProductDetailResponse>("/api/catalog/products/competition-kettlebell-24-kg", Json, Ct);

        Assert.Equal(StockStatus.LowStock, product!.StockStatus);
        Assert.Equal(3, product.MaxOrderQuantity);
    }

    [Theory]
    [InlineData("/api/catalog/products/no-such-product")]
    [InlineData("/api/catalog/products/no-such-product/related")]
    public async Task Unknown_product_slug_returns_404_problem_details(string url)
    {
        await using var api = await NewApiAsync();
        var client = api.Client;

        var response = await client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Archived_products_disappear_from_lists_detail_related_and_brand_counts()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var db = PostgresFixture.CreateDbContext(connectionString))
        {
            await db.Products.Where(p => p.Slug == "whey-isolate-chocolate-2-kg")
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), Ct);
        }
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();

        var list = await ListAsync(client, "category=protein&pageSize=48");
        var detail = await client.GetAsync("/api/catalog/products/whey-isolate-chocolate-2-kg", Ct);
        var related = await client.GetFromJsonAsync<List<ProductCardResponse>>("/api/catalog/products/whey-isolate-vanilla-2-kg/related", Json, Ct);
        var halvard = await client.GetFromJsonAsync<BrandResponse>("/api/catalog/brands/halvard-nutrition", Json, Ct);

        Assert.DoesNotContain(list.Items, i => i.Slug == "whey-isolate-chocolate-2-kg");
        Assert.Equal(7, list.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.DoesNotContain(related!, r => r.Slug == "whey-isolate-chocolate-2-kg");
        Assert.Equal(CatalogSeedCount("halvard-nutrition") - 1, halvard!.ProductCount);
    }

    [Fact]
    public async Task Images_are_cached_immutably_and_revalidate_with_the_etag()
    {
        await using var api = await NewApiAsync();
        var client = api.Client;
        var product = await client.GetFromJsonAsync<ProductDetailResponse>("/api/catalog/products/whey-isolate-vanilla-2-kg", Json, Ct);

        var first = await client.GetAsync(product!.ImageUrl, Ct);
        var revalidate = new HttpRequestMessage(HttpMethod.Get, product.ImageUrl);
        revalidate.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await client.SendAsync(revalidate, Ct);
        var missing = await client.GetAsync($"/api/images/{Guid.CreateVersion7()}", Ct);

        Assert.Equal("public, max-age=31536000, immutable", first.Headers.CacheControl?.ToString());
        Assert.False(first.Headers.ETag!.IsWeak);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Null(missing.Headers.CacheControl?.MaxAge);
    }

    private static int CatalogSeedCount(string brand) =>
        FitnessMerchant.Api.Infrastructure.Seed.CatalogSeedData.Products.Count(p => p.Brand == brand);

    private sealed class TestApi(ApiFactory factory, List<OracleProduct> oracle) : IAsyncDisposable
    {
        public HttpClient Client { get; } = factory.CreateClient();
        public List<OracleProduct> Oracle { get; } = oracle;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await factory.DisposeAsync();
        }
    }

    private async Task<TestApi> NewApiAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = PostgresFixture.CreateDbContext(connectionString);
        var oracle = (await db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Brand).ToListAsync(Ct))
            .Select(p => new OracleProduct(
                p.Id, p.Slug, p.Name, p.Category.Slug, p.Brand.Slug, p.Brand.Name, p.Category.Department,
                p.Price, p.DiscountPercent, Pricing.SalePrice(p.Price, p.DiscountPercent), p.StockQuantity, p.CreatedAt, p.IsActive))
            .ToList();
        return new TestApi(new ApiFactory(connectionString), oracle);
    }

    private static async Task<PagedResponse<ProductCardResponse>> ListAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/catalog/products?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResponse<ProductCardResponse>>(Json, Ct))!;
    }

    private static List<OracleProduct> ApplyFilters(List<OracleProduct> products, string query)
    {
        var parameters = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToLookup(pair => pair[0], pair => pair[1]);
        decimal? Decimal(string key) => parameters[key].Select(v => (decimal?)decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault();

        var result = products.Where(p => p.IsActive);
        if (parameters["department"].FirstOrDefault() is { } department)
        {
            result = result.Where(p => p.Department.ToString().Equals(department, StringComparison.OrdinalIgnoreCase));
        }
        if (parameters["category"].Any())
        {
            result = result.Where(p => parameters["category"].Contains(p.CategorySlug));
        }
        if (parameters["brand"].Any())
        {
            result = result.Where(p => parameters["brand"].Contains(p.BrandSlug));
        }
        if (Decimal("minPrice") is { } min)
        {
            result = result.Where(p => p.SalePrice >= min);
        }
        if (Decimal("maxPrice") is { } max)
        {
            result = result.Where(p => p.SalePrice <= max);
        }
        if (parameters["inStock"].FirstOrDefault() == "true")
        {
            result = result.Where(p => p.Stock > 0);
        }
        return result.ToList();
    }
}
