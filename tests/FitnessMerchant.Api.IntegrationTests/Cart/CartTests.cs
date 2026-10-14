using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessMerchant.Api.Features.Cart;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.IntegrationTests.Cart;

public class CartTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    // Seed products used below (CatalogSeedData): prices after discount, stock.
    private const string Whey = "whey-isolate-vanilla-2-kg";      // 53.91, stock 42
    private const string Creatine = "creatine-monohydrate-500-g";  // 24.90, stock 80
    private const string Kettlebell = "competition-kettlebell-24-kg"; // 80.67, stock 3
    private const string Magnesium = "magnesium-bisglycinate-x-120"; // stock 0

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cart_endpoints_require_a_signed_in_user_except_the_quote()
    {
        await using var api = await StartAsync();
        using var guest = api.Factory.CreateBrowserClient();
        await guest.MeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.Http.GetAsync("/api/cart", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Put(guest, api.Id(Whey), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.Http.PostAsJsonAsync("/api/cart/merge", Items((api.Id(Whey), 1)), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.Http.PostAsJsonAsync("/api/cart/quote", Items((api.Id(Whey), 1)), Ct)).StatusCode);
    }

    [Fact]
    public async Task Add_update_remove_and_clear_with_server_side_totals()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerAsync();

        var empty = await GetCartAsync(customer);
        Assert.Empty(empty.Lines);
        Assert.Equal((0m, 0m, 0m), (empty.Subtotal, empty.Shipping, empty.Total));

        var added = await ReadCart(await Put(customer, api.Id(Whey), 2));
        var line = Assert.Single(added.Lines);
        Assert.Equal((2, 53.91m, 107.82m, (CartLineIssue?)null), (line.Quantity, line.UnitPrice, line.LineTotal, line.Issue));
        Assert.Equal((2, 107.82m, 0m, 107.82m), (added.ItemCount, added.Subtotal, added.Shipping, added.Total));

        var updated = await ReadCart(await Put(customer, api.Id(Whey), 1));
        Assert.Equal(1, Assert.Single(updated.Lines).Quantity);

        await Put(customer, api.Id(Creatine), 1);
        var removed = await ReadCart(await customer.Http.DeleteAsync($"/api/cart/items/{api.Id(Whey)}", Ct));
        Assert.Equal(Creatine, Assert.Single(removed.Lines).Slug);
        Assert.Equal((24.90m, 4.90m, 29.80m), (removed.Subtotal, removed.Shipping, removed.Total));

        var cleared = await ReadCart(await customer.Http.DeleteAsync("/api/cart", Ct));
        Assert.Empty(cleared.Lines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Quantity_outside_1_to_10_is_a_validation_error(int quantity)
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerAsync();

        var response = await Put(customer, api.Id(Whey), quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Quantity_is_capped_at_stock_with_the_limit_in_the_problem()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerAsync();

        var tooMany = await Put(customer, api.Id(Kettlebell), 4);
        var exact = await Put(customer, api.Id(Kettlebell), 3);

        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
        var problem = await tooMany.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Only 3 available.", problem!.Title);
        Assert.Equal(3, ((JsonElement)problem.Extensions["maxQuantity"]!).GetInt32());
        Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
    }

    [Fact]
    public async Task Archived_out_of_stock_and_unknown_products_are_rejected()
    {
        await using var api = await StartAsync();
        await api.SqlAsync(db => db.Products.Where(p => p.Slug == Creatine).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), Ct));
        using var customer = await api.CustomerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Put(customer, api.Id(Creatine), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Put(customer, api.Id(Magnesium), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(customer, Guid.CreateVersion7(), 1)).StatusCode);
        Assert.Empty((await GetCartAsync(customer)).Lines);
    }

    [Fact]
    public async Task Merge_sums_quantities_caps_them_and_skips_stale_items()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerAsync();
        await Put(customer, api.Id(Creatine), 2);

        var response = await customer.Http.PostAsJsonAsync("/api/cart/merge", Items(
            (api.Id(Creatine), 5), (api.Id(Creatine), 4), // combined 9, plus 2 already = 11 -> capped at 10
            (api.Id(Kettlebell), 5),                      // stock 3
            (api.Id(Magnesium), 1),                       // out of stock: skipped
            (Guid.CreateVersion7(), 1)), Ct);             // unknown: skipped

        var cart = await ReadCart(response);
        Assert.Equal(
            [(Creatine, 10), (Kettlebell, 3)],
            cart.Lines.Select(l => (l.Slug, l.Quantity)).OrderBy(l => l.Slug, StringComparer.Ordinal).Reverse());
        Assert.All(cart.Lines, l => Assert.Null(l.Issue));
    }

    [Fact]
    public async Task Each_user_only_ever_reaches_their_own_cart()
    {
        await using var api = await StartAsync();
        using var alice = await api.CustomerAsync();
        using var bob = await api.CustomerAsync();
        await Put(alice, api.Id(Whey), 2);

        var bobsView = await GetCartAsync(bob);
        await bob.Http.DeleteAsync($"/api/cart/items/{api.Id(Whey)}", Ct);
        await bob.Http.DeleteAsync("/api/cart", Ct);
        await Put(bob, api.Id(Creatine), 1);

        Assert.Empty(bobsView.Lines);
        var alicesCart = await GetCartAsync(alice);
        Assert.Equal((Whey, 2), (Assert.Single(alicesCart.Lines).Slug, alicesCart.Lines[0].Quantity));
        Assert.Equal(Creatine, Assert.Single((await GetCartAsync(bob)).Lines).Slug);
    }

    [Theory]
    [InlineData("59.99", "4.90", "64.89")]
    [InlineData("60.00", "0", "60.00")]
    [InlineData("60.01", "0", "60.01")]
    public async Task Shipping_threshold_at_the_boundary(string price, string shipping, string total)
    {
        await using var api = await StartAsync();
        var value = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        await api.SqlAsync(db => db.Products.Where(p => p.Slug == Whey)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, value).SetProperty(p => p.DiscountPercent, 0), Ct));
        using var customer = await api.CustomerAsync();

        var cart = await ReadCart(await Put(customer, api.Id(Whey), 1));

        Assert.Equal(value, cart.Subtotal);
        Assert.Equal(decimal.Parse(shipping, System.Globalization.CultureInfo.InvariantCulture), cart.Shipping);
        Assert.Equal(decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture), cart.Total);
    }

    [Fact]
    public async Task Lines_warn_when_stock_drops_or_the_product_is_archived_and_totals_follow()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerAsync();
        await Put(customer, api.Id(Kettlebell), 3);
        await Put(customer, api.Id(Creatine), 1);
        await Put(customer, api.Id(Whey), 1);

        await api.SqlAsync(db => db.Products.Where(p => p.Slug == Kettlebell).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 1), Ct));
        await api.SqlAsync(db => db.Products.Where(p => p.Slug == Creatine).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 0), Ct));
        await api.SqlAsync(db => db.Products.Where(p => p.Slug == Whey).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), Ct));
        var cart = await GetCartAsync(customer);

        var bySlug = cart.Lines.ToDictionary(l => l.Slug);
        Assert.Equal((CartLineIssue.ExceedsStock, 3, 1, 80.67m), (bySlug[Kettlebell].Issue!.Value, bySlug[Kettlebell].Quantity, bySlug[Kettlebell].MaxQuantity, bySlug[Kettlebell].LineTotal));
        Assert.Equal((CartLineIssue.OutOfStock, 0m), (bySlug[Creatine].Issue!.Value, bySlug[Creatine].LineTotal));
        Assert.Equal((CartLineIssue.Unavailable, 0m), (bySlug[Whey].Issue!.Value, bySlug[Whey].LineTotal));
        Assert.Equal((1, 80.67m, 0m), (cart.ItemCount, cart.Subtotal, cart.Shipping));
    }

    [Fact]
    public async Task Guest_quote_prices_items_without_storing_anything()
    {
        await using var api = await StartAsync();
        using var guest = api.Factory.CreateBrowserClient();
        await guest.MeAsync();

        var quote = await ReadCart(await guest.Http.PostAsJsonAsync("/api/cart/quote", Items((api.Id(Whey), 1), (api.Id(Kettlebell), 9), (api.Id(Whey), 1)), Ct));

        Assert.Equal(2, quote.Lines.Count);
        Assert.Equal(2, quote.Lines.Single(l => l.Slug == Whey).Quantity); // duplicates combined
        Assert.Equal(CartLineIssue.ExceedsStock, quote.Lines.Single(l => l.Slug == Kettlebell).Issue);
        Assert.Equal(2 * 53.91m + 3 * 80.67m, quote.Subtotal);
        Assert.Equal(0, await api.SqlAsync(db => db.CartItems.CountAsync(Ct)));
    }

    [Fact]
    public async Task Guest_quotes_are_rate_limited()
    {
        await using var api = await StartAsync(new Dictionary<string, string?> { ["RateLimiting:QuotePermitLimit"] = "2" });
        using var guest = api.Factory.CreateBrowserClient();
        await guest.MeAsync();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await guest.Http.PostAsJsonAsync("/api/cart/quote", Items((api.Id(Whey), 1)), Ct)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Concurrent_writes_to_the_same_line_never_fail()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerAsync();

        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(q => Put(customer, api.Id(Whey), q)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var cart = await GetCartAsync(customer);
        Assert.InRange(Assert.Single(cart.Lines).Quantity, 1, 10);
        Assert.Equal(1, await api.SqlAsync(db => db.Carts.CountAsync(Ct)));
    }

    private static CartItemsRequest Items(params (Guid ProductId, int Quantity)[] items) =>
        new(items.Select(i => new CartItemInput(i.ProductId, i.Quantity)).ToList());

    private static Task<HttpResponseMessage> Put(BrowserClient client, Guid productId, int quantity) =>
        client.Http.PutAsJsonAsync($"/api/cart/items/{productId}", new { quantity }, Ct);

    private static async Task<CartResponse> GetCartAsync(BrowserClient client) =>
        await ReadCart(await client.Http.GetAsync("/api/cart", Ct));

    private static async Task<CartResponse> ReadCart(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartResponse>(Json, Ct))!;
    }

    private async Task<TestApi> StartAsync(IReadOnlyDictionary<string, string?>? settings = null)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = PostgresFixture.CreateDbContext(connectionString);
        var ids = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Id, Ct);
        return new TestApi(new ApiFactory(connectionString, settings), connectionString, ids);
    }

    private sealed class TestApi(ApiFactory factory, string connectionString, Dictionary<string, Guid> ids) : IAsyncDisposable
    {
        public ApiFactory Factory { get; } = factory;

        public Guid Id(string slug) => ids[slug];

        public async Task<BrowserClient> CustomerAsync()
        {
            var client = Factory.CreateBrowserClient();
            Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(BrowserClient.NewEmail())).StatusCode);
            return client;
        }

        public async Task<T> SqlAsync<T>(Func<Api.Infrastructure.Data.AppDbContext, Task<T>> action)
        {
            await using var db = PostgresFixture.CreateDbContext(connectionString);
            return await action(db);
        }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
