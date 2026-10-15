using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessMerchant.Api.Features.Admin;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Features.Orders;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessMerchant.Api.IntegrationTests.Admin;

public class AdminCatalogTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    private const string Whey = "whey-isolate-vanilla-2-kg";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_admin_endpoint_is_401_for_guests_and_403_for_customers()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var guest = factory.CreateBrowserClient();
        await guest.MeAsync(); // has an XSRF token, so antiforgery is not what stops it
        using var customer = factory.CreateBrowserClient();
        await customer.RegisterAsync(BrowserClient.NewEmail());

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/admin", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (Method: m, Route: e.RoutePattern.RawText!)))
            .ToList();
        Assert.True(routes.Count >= 17, $"expected the whole admin route table, found {routes.Count}");

        var failures = new List<string>();
        foreach (var (method, route) in routes)
        {
            var path = route.Replace("{id:guid}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
            foreach (var (client, expected) in new[] { (guest, HttpStatusCode.Unauthorized), (customer, HttpStatusCode.Forbidden) })
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
                var status = (await client.Http.SendAsync(request, Ct)).StatusCode;
                if (status != expected)
                {
                    failures.Add($"{method} {route}: {status}, expected {expected}");
                }
            }
        }
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Create_reports_every_invalid_field_at_once()
    {
        await using var api = await StartAsync();

        var response = await api.Admin.Http.PostAsJsonAsync("/api/admin/products", new
        {
            sku = "bad sku!",
            name = "X",
            slug = "Not A Slug",
            description = "",
            price = 10.999m,
            discountPercent = 95,
            stockQuantity = -1,
        }, Ct);

        var errors = await ValidationErrorsAsync(response);
        Assert.Equal(
            ["brandId", "categoryId", "description", "discountPercent", "name", "price", "sku", "slug", "stockQuantity"],
            errors.Keys.Order(StringComparer.Ordinal));
        Assert.Contains("e.g. not-a-slug", errors["slug"][0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_checks_references_and_unique_values_against_the_database()
    {
        await using var api = await StartAsync();

        var response = await api.Admin.Http.PostAsJsonAsync("/api/admin/products",
            api.NewProduct() with { Sku = "hal-0001", Slug = Whey, CategoryId = Guid.NewGuid(), ImageId = Guid.NewGuid() }, Ct);

        var errors = await ValidationErrorsAsync(response);
        Assert.Equal(["categoryId", "imageId", "sku", "slug"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Another product already uses this SKU."], errors["sku"]); // compared after upper-casing
    }

    [Fact]
    public async Task A_created_product_is_on_the_storefront_with_a_generated_unique_slug()
    {
        await using var api = await StartAsync();

        // Same name as a seeded product: the generated slug gets a suffix instead of failing.
        var response = await api.Admin.Http.PostAsJsonAsync("/api/admin/products",
            api.NewProduct() with { Name = "Whey Isolate Vanilla 2 kg", Sku = " fm-test-1 " }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AdminProductResponse>(Json, Ct))!;
        Assert.Equal(($"{Whey}-2", "FM-TEST-1", 44.91m), (created.Slug, created.Sku, created.SalePrice));
        Assert.Equal($"/api/admin/products/{created.Id}", response.Headers.Location?.ToString());

        using var shopper = api.Factory.CreateBrowserClient();
        var detail = await shopper.Http.GetFromJsonAsync<ProductDetailResponse>($"/api/catalog/products/{created.Slug}", Json, Ct);
        Assert.Equal("Whey Isolate Vanilla 2 kg", detail!.Name);
    }

    [Fact]
    public async Task Admin_list_searches_filters_sorts_and_pages_in_sql()
    {
        await using var api = await StartAsync();
        await api.Db(db => db.Products.Where(p => p.Slug == Whey).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), Ct));

        var archived = await api.Admin.Http.GetFromJsonAsync<PagedResponse<AdminProductRow>>("/api/admin/products?status=archived", Json, Ct);
        var bySku = await api.Admin.Http.GetFromJsonAsync<PagedResponse<AdminProductRow>>("/api/admin/products?q=hal-000&sort=sku-asc&pageSize=3", Json, Ct);
        var cheapest = await api.Admin.Http.GetFromJsonAsync<PagedResponse<AdminProductRow>>("/api/admin/products?sort=price-asc&pageSize=48", Json, Ct);
        var badSort = await api.Admin.Http.GetAsync("/api/admin/products?sort=price;drop table products", Ct);

        Assert.Equal(Whey, Assert.Single(archived!.Items).Slug);
        Assert.Equal(3, bySku!.Items.Count);
        Assert.Equal(bySku.Items.Select(r => r.Sku).Order(StringComparer.Ordinal), bySku.Items.Select(r => r.Sku));
        Assert.All(bySku.Items, r => Assert.StartsWith("HAL-000", r.Sku, StringComparison.Ordinal));
        Assert.True(bySku.TotalCount > 3);
        Assert.Equal(cheapest!.Items.Select(r => r.Price).Order(), cheapest.Items.Select(r => r.Price));
        Assert.Contains("sort", (await ValidationErrorsAsync(badSort)).Keys);
    }

    [Fact]
    public async Task Update_saves_and_a_stale_version_gets_409_without_overwriting()
    {
        await using var api = await StartAsync();
        var id = await api.ProductIdAsync(Whey);
        var original = (await api.Admin.Http.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{id}", Json, Ct))!;

        var first = await api.Admin.Http.PutAsJsonAsync($"/api/admin/products/{id}", Edit(original) with { Price = 59.00m }, Ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var saved = (await first.Content.ReadFromJsonAsync<AdminProductResponse>(Json, Ct))!;
        Assert.Equal(59.00m, saved.Price);
        Assert.NotEqual(original.Version, saved.Version);
        Assert.True(saved.UpdatedAt > original.UpdatedAt);

        // A second admin still has the form from before the first save.
        var stale = await api.Admin.Http.PutAsJsonAsync($"/api/admin/products/{id}", Edit(original) with { Price = 1.00m }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency", ((JsonElement)(await stale.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Extensions["code"]!).GetString());
        Assert.Equal(59.00m, (await api.Admin.Http.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{id}", Json, Ct))!.Price);
    }

    [Fact]
    public async Task Update_without_a_version_or_of_a_missing_product_is_rejected()
    {
        await using var api = await StartAsync();
        var id = await api.ProductIdAsync(Whey);
        var product = (await api.Admin.Http.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{id}", Json, Ct))!;

        var noVersion = await api.Admin.Http.PutAsJsonAsync($"/api/admin/products/{id}", Edit(product) with { Version = null }, Ct);
        var missing = await api.Admin.Http.PutAsJsonAsync($"/api/admin/products/{Guid.NewGuid()}", Edit(product), Ct);

        Assert.Contains("version", (await ValidationErrorsAsync(noVersion)).Keys);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Clearing_the_slug_keeps_it_unless_the_name_changed()
    {
        await using var api = await StartAsync();
        var id = await api.ProductIdAsync(Whey);
        var product = (await api.Admin.Http.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{id}", Json, Ct))!;

        var same = (await (await api.Admin.Http.PutAsJsonAsync($"/api/admin/products/{id}", Edit(product) with { Slug = "" }, Ct))
            .Content.ReadFromJsonAsync<AdminProductResponse>(Json, Ct))!;
        var renamed = (await (await api.Admin.Http.PutAsJsonAsync($"/api/admin/products/{id}", Edit(same) with { Slug = "", Name = "Whey Isolate Mango 2 kg" }, Ct))
            .Content.ReadFromJsonAsync<AdminProductResponse>(Json, Ct))!;

        Assert.Equal(Whey, same.Slug);
        Assert.Equal("whey-isolate-mango-2-kg", renamed.Slug);
    }

    [Fact]
    public async Task Deleting_a_never_ordered_product_removes_it_with_its_cart_lines()
    {
        await using var api = await StartAsync();
        var created = (await (await api.Admin.Http.PostAsJsonAsync("/api/admin/products", api.NewProduct(), Ct))
            .Content.ReadFromJsonAsync<AdminProductResponse>(Json, Ct))!;
        using var shopper = api.Factory.CreateBrowserClient();
        await shopper.RegisterAsync(BrowserClient.NewEmail());
        (await shopper.Http.PutAsJsonAsync($"/api/cart/items/{created.Id}", new { quantity = 1 }, Ct)).EnsureSuccessStatusCode();

        var response = await api.Admin.Http.DeleteAsync($"/api/admin/products/{created.Id}", Ct);

        Assert.Equal(DeleteOutcome.Deleted, (await response.Content.ReadFromJsonAsync<DeleteProductResponse>(Json, Ct))!.Outcome);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Admin.Http.GetAsync($"/api/admin/products/{created.Id}", Ct)).StatusCode);
        Assert.Equal(0, await api.Db(db => db.CartItems.CountAsync(i => i.ProductId == created.Id, Ct)));
    }

    [Fact]
    public async Task Deleting_an_ordered_product_archives_it_off_the_storefront_while_the_order_still_shows_it()
    {
        await using var api = await StartAsync();
        var id = await api.ProductIdAsync(Whey);
        using var shopper = api.Factory.CreateBrowserClient();
        await shopper.RegisterAsync(BrowserClient.NewEmail());
        (await shopper.Http.PutAsJsonAsync($"/api/cart/items/{id}", new { quantity = 1 }, Ct)).EnsureSuccessStatusCode();
        using var checkout = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
        {
            Content = JsonContent.Create(new CheckoutRequest(
                new ShippingInput("Test Shopper", "10 Example Street", null, "Leipzig", "04109", "Germany", "+49 000 000 000", "shopper@test.example"),
                new PaymentInput(FakePaymentGateway.SuccessCard, "12/30", "123", "Test Shopper"), null)),
        };
        checkout.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var order = (await (await shopper.Http.SendAsync(checkout, Ct)).Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;

        var response = await api.Admin.Http.DeleteAsync($"/api/admin/products/{id}", Ct);

        Assert.Equal(DeleteOutcome.Archived, (await response.Content.ReadFromJsonAsync<DeleteProductResponse>(Json, Ct))!.Outcome);
        Assert.Equal(HttpStatusCode.NotFound, (await shopper.Http.GetAsync($"/api/catalog/products/{Whey}", Ct)).StatusCode);
        var listing = await shopper.Http.GetFromJsonAsync<PagedResponse<ProductCardResponse>>("/api/catalog/products?q=whey&pageSize=48", Json, Ct);
        Assert.DoesNotContain(listing!.Items, p => p.Slug == Whey);
        var stillOrdered = (await shopper.Http.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Number}", Json, Ct))!.Items.Single();
        Assert.Equal(("Whey Isolate Vanilla 2 kg", false), (stillOrdered.ProductName, stillOrdered.ProductAvailable));
        Assert.False((await api.Admin.Http.GetFromJsonAsync<AdminProductResponse>($"/api/admin/products/{id}", Json, Ct))!.IsActive);
    }

    [Fact]
    public async Task Categories_and_brands_in_use_cannot_be_deleted_but_empty_ones_can()
    {
        await using var api = await StartAsync();
        var categories = (await api.Admin.Http.GetFromJsonAsync<List<AdminCategoryResponse>>("/api/admin/categories", Json, Ct))!;
        var brands = (await api.Admin.Http.GetFromJsonAsync<List<AdminBrandResponse>>("/api/admin/brands", Json, Ct))!;
        var usedCategory = categories.First(c => c.ProductCount > 0);
        var usedBrand = brands.First(b => b.ProductCount > 0);

        foreach (var (path, count) in new[] { ($"/api/admin/categories/{usedCategory.Id}", usedCategory.ProductCount), ($"/api/admin/brands/{usedBrand.Id}", usedBrand.ProductCount) })
        {
            var blocked = await api.Admin.Http.DeleteAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var problem = (await blocked.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!;
            Assert.Equal("in-use", ((JsonElement)problem.Extensions["code"]!).GetString());
            Assert.Equal(count, ((JsonElement)problem.Extensions["productCount"]!).GetInt32());
        }

        var newCategory = await api.Admin.Http.PostAsJsonAsync("/api/admin/categories", new { department = "gear", name = "Jump Ropes" }, Ct);
        var newBrand = await api.Admin.Http.PostAsJsonAsync("/api/admin/brands", new { name = "Testbrand Labs", description = "Invented.", accentColor = "#1F6FEB" }, Ct);
        Assert.Equal(HttpStatusCode.Created, newCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Created, newBrand.StatusCode);
        var category = (await newCategory.Content.ReadFromJsonAsync<AdminCategoryResponse>(Json, Ct))!;
        var brand = (await newBrand.Content.ReadFromJsonAsync<AdminBrandResponse>(Json, Ct))!;
        Assert.Equal(("jump-ropes", "testbrand-labs", "#1f6feb"), (category.Slug, brand.Slug, brand.AccentColor));

        Assert.Equal(HttpStatusCode.NoContent, (await api.Admin.Http.DeleteAsync($"/api/admin/categories/{category.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Admin.Http.DeleteAsync($"/api/admin/brands/{brand.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Category_and_brand_input_is_validated_and_names_and_slugs_stay_unique()
    {
        await using var api = await StartAsync();
        var existingBrand = (await api.Admin.Http.GetFromJsonAsync<List<AdminBrandResponse>>("/api/admin/brands", Json, Ct))![0];
        var existingCategory = (await api.Admin.Http.GetFromJsonAsync<List<AdminCategoryResponse>>("/api/admin/categories", Json, Ct))![0];

        var category = await api.Admin.Http.PostAsJsonAsync("/api/admin/categories", new { name = "", slug = existingCategory.Slug, sortOrder = -1 }, Ct);
        var brand = await api.Admin.Http.PostAsJsonAsync("/api/admin/brands", new { name = existingBrand.Name, description = "x", accentColor = "red" }, Ct);
        var takenSlug = await api.Admin.Http.PostAsJsonAsync("/api/admin/brands", new { name = "Fresh Brand", slug = existingBrand.Slug, description = "x", accentColor = "#000000" }, Ct);

        Assert.Equal(["department", "name", "sortOrder"], (await ValidationErrorsAsync(category)).Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["accentColor"], (await ValidationErrorsAsync(brand)).Keys);
        Assert.Equal(["slug"], (await ValidationErrorsAsync(takenSlug)).Keys);
        // The duplicate brand name is found once the shape is valid.
        var duplicateName = await api.Admin.Http.PostAsJsonAsync("/api/admin/brands", new { name = existingBrand.Name, description = "x", accentColor = "#000000" }, Ct);
        Assert.Equal(["name"], (await ValidationErrorsAsync(duplicateName)).Keys);
    }

    // --- helpers ---

    private static ProductInput Edit(AdminProductResponse p) => new(
        p.Sku, p.Name, p.Slug, p.Description, p.CategoryId, p.BrandId, p.Price, p.DiscountPercent, p.StockQuantity, p.IsActive, p.ImageId, p.Version);

    private static async Task<IDictionary<string, string[]>> ValidationErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!.Errors;
    }

    private async Task<TestApi> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var factory = new ApiFactory(connectionString);
        var admin = await factory.CreateAdminClientAsync();
        await using var db = PostgresFixture.CreateDbContext(connectionString);
        var category = await db.Categories.Where(c => c.Slug == "protein").Select(c => c.Id).SingleAsync(Ct);
        var brand = await db.Brands.OrderBy(b => b.Name).Select(b => b.Id).FirstAsync(Ct);
        return new TestApi(factory, admin, connectionString, category, brand);
    }

    private sealed class TestApi(ApiFactory factory, BrowserClient admin, string connectionString, Guid categoryId, Guid brandId) : IAsyncDisposable
    {
        public ApiFactory Factory { get; } = factory;
        public BrowserClient Admin { get; } = admin;

        public ProductInput NewProduct() => new(
            $"FM-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), "Test Lifting Straps", null, "Invented test product.",
            categoryId, brandId, 49.90m, 10, 25, true, null, null);

        public Task<Guid> ProductIdAsync(string slug) => Db(db => db.Products.Where(p => p.Slug == slug).Select(p => p.Id).SingleAsync(Ct));

        public async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
        {
            await using var db = PostgresFixture.CreateDbContext(connectionString);
            return await action(db);
        }

        public async ValueTask DisposeAsync()
        {
            Admin.Dispose();
            await Factory.DisposeAsync();
        }
    }
}
