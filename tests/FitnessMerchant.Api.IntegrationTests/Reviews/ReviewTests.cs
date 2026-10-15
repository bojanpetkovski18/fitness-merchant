using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Features.Admin;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Features.Orders;
using FitnessMerchant.Api.Features.Reviews;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.IntegrationTests.Reviews;

public class ReviewTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Only_buyers_of_a_paid_order_may_review()
    {
        await using var api = await StartAsync();
        using var guest = api.Factory.CreateBrowserClient();
        using var stranger = await api.CustomerAsync();
        using var cancelledBuyer = await api.CustomerAsync();
        var cancelled = await api.BuyAsync(cancelledBuyer);
        (await cancelledBuyer.Http.PostAsync($"/api/orders/{cancelled}/cancel", null, Ct)).EnsureSuccessStatusCode();

        Assert.Equal(ReviewEligibility.SignIn, (await api.ReviewsAsync(guest)).Viewer);
        Assert.Equal(ReviewEligibility.NotPurchased, (await api.ReviewsAsync(stranger)).Viewer);
        Assert.Equal(ReviewEligibility.NotPurchased, (await api.ReviewsAsync(cancelledBuyer)).Viewer);

        await guest.MeAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.PostReviewAsync(guest, 5)).StatusCode);
        foreach (var client in new[] { stranger, cancelledBuyer })
        {
            var response = await api.PostReviewAsync(client, 5);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-eligible", ((JsonElement)(await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Extensions["code"]!).GetString());
        }
        Assert.Equal(0, await api.Db(db => db.Reviews.CountAsync(r => r.ProductId == api.ProductId, Ct)));
    }

    [Fact]
    public async Task A_buyer_reviews_once_and_the_rating_follows_from_sql()
    {
        await using var api = await StartAsync();
        using var buyer = await api.CustomerAsync("Mila Novak");
        await api.BuyAsync(buyer);
        Assert.Equal(ReviewEligibility.CanReview, (await api.ReviewsAsync(buyer)).Viewer);

        var first = await api.PostReviewAsync(buyer, 4, title: "  Solid straps  ");
        var second = await api.PostReviewAsync(buyer, 1);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = (await first.Content.ReadFromJsonAsync<ReviewResponse>(Json, Ct))!;
        Assert.Equal(("Solid straps", "Mila Novak", 4), (created.Title, created.Author, created.Rating));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("already-reviewed", ((JsonElement)(await second.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Extensions["code"]!).GetString());

        var reviews = await api.ReviewsAsync(buyer);
        Assert.Equal(ReviewEligibility.AlreadyReviewed, reviews.Viewer);
        Assert.Equal((4.00m, 1), (reviews.Summary.Average, reviews.Summary.Count));
        Assert.Equal([0, 1, 0, 0, 0], reviews.Summary.Distribution); // 5 stars down to 1
        var detail = await buyer.Http.GetFromJsonAsync<ProductDetailResponse>($"/api/catalog/products/{api.Slug}", Json, Ct);
        Assert.Equal((4.00m, 1), (detail!.RatingAverage, detail.RatingCount));
    }

    [Fact]
    public async Task Hiding_and_unhiding_recomputes_the_rating()
    {
        await using var api = await StartAsync();
        using var admin = await api.Factory.CreateAdminClientAsync();
        foreach (var rating in new[] { 5, 4, 2 })
        {
            using var buyer = await api.CustomerAsync();
            await api.BuyAsync(buyer);
            (await api.PostReviewAsync(buyer, rating)).EnsureSuccessStatusCode();
        }
        Assert.Equal((3.67m, 3), await api.RatingAsync());
        var low = await api.Db(db => db.Reviews.Where(r => r.ProductId == api.ProductId && r.Rating == 2).Select(r => r.Id).SingleAsync(Ct));

        var hide = await admin.Http.PutAsJsonAsync($"/api/admin/reviews/{low}/visibility", new { isHidden = true }, Ct);
        Assert.True((await hide.Content.ReadFromJsonAsync<AdminReviewRow>(Json, Ct))!.IsHidden);
        Assert.Equal((4.50m, 2), await api.RatingAsync());
        using var anyone = api.Factory.CreateBrowserClient();
        Assert.DoesNotContain((await api.ReviewsAsync(anyone)).Reviews.Items, r => r.Id == low);

        (await admin.Http.PutAsJsonAsync($"/api/admin/reviews/{low}/visibility", new { isHidden = false }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal((3.67m, 3), await api.RatingAsync());
        // The listing card shows the same number.
        var cards = await anyone.Http.GetFromJsonAsync<PagedResponse<ProductCardResponse>>($"/api/catalog/products?q={Uri.EscapeDataString(api.Name)}", Json, Ct);
        Assert.Equal((3.67m, 3), cards!.Items.Where(c => c.Slug == api.Slug).Select(c => (c.RatingAverage, c.RatingCount)).Single());
    }

    [Fact]
    public async Task Concurrent_reviews_of_one_product_all_count()
    {
        await using var api = await StartAsync();
        var buyers = new List<BrowserClient>();
        for (var i = 0; i < 5; i++)
        {
            var buyer = await api.CustomerAsync();
            await api.BuyAsync(buyer);
            buyers.Add(buyer);
        }

        var responses = await Task.WhenAll(buyers.Select((b, i) => api.PostReviewAsync(b, i + 1)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal((3.00m, 5), await api.RatingAsync());
        buyers.ForEach(b => b.Dispose());
    }

    [Fact]
    public async Task Html_in_a_review_is_stored_and_returned_as_plain_text_and_emails_never_leak()
    {
        await using var api = await StartAsync();
        using var buyer = await api.CustomerAsync("Sam Doe");
        await api.BuyAsync(buyer);
        const string body = "<script>alert('x')</script> <b>Great</b> straps & strong stitching.";

        var post = await api.PostReviewAsync(buyer, 5, body: body);

        var postText = await post.Content.ReadAsStringAsync(Ct);
        using var anyone = api.Factory.CreateBrowserClient();
        var getText = await anyone.Http.GetStringAsync($"/api/catalog/products/{api.Slug}/reviews", Ct);
        Assert.Equal(body, (await api.ReviewsAsync(anyone)).Reviews.Items.Single().Body);
        Assert.Equal(body, await api.Db(db => db.Reviews.Where(r => r.ProductId == api.ProductId).Select(r => r.Body).SingleAsync(Ct)));
        Assert.DoesNotContain("@test.example", postText, StringComparison.Ordinal);
        Assert.DoesNotContain("@test.example", getText, StringComparison.Ordinal);
        Assert.DoesNotContain("email", getText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Review_input_is_validated_after_trimming()
    {
        await using var api = await StartAsync();
        using var buyer = await api.CustomerAsync();
        await api.BuyAsync(buyer);

        var response = await api.PostReviewAsync(buyer, 6, title: "  ok  ", body: "too short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["body", "rating", "title"], (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Posting_reviews_is_rate_limited()
    {
        await using var api = await StartAsync(new Dictionary<string, string?> { ["RateLimiting:ReviewPermitLimit"] = "2" });
        using var buyer = await api.CustomerAsync();

        await api.PostReviewAsync(buyer, 0);
        await api.PostReviewAsync(buyer, 0);
        var limited = await api.PostReviewAsync(buyer, 0);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Seeded_reviews_come_from_paid_buyers_and_ratings_match_sql()
    {
        await using var api = await StartAsync();

        var orphans = await api.Db(db => db.Reviews.CountAsync(r => !db.Orders.Any(o =>
            o.UserId == r.UserId && new[] { OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered }.Contains(o.Status) &&
            o.Items.Any(i => i.ProductId == r.ProductId)), Ct));
        var mismatches = await api.Db(db => db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM products p
            LEFT JOIN (SELECT product_id, COUNT(*) AS n, ROUND(AVG(rating), 2) AS avg FROM reviews WHERE NOT is_hidden GROUP BY product_id) r
                ON r.product_id = p.id
            WHERE p.rating_count <> COALESCE(r.n, 0) OR p.rating_average <> COALESCE(r.avg, 0)
            """).SingleAsync(Ct));

        Assert.True(await api.Db(db => db.Reviews.CountAsync(Ct)) > 20, "the seed should rate a good part of the catalog");
        Assert.Equal(0, orphans);
        Assert.Equal(0, mismatches);
    }

    // --- helpers ---

    private async Task<TestApi> StartAsync(Dictionary<string, string?>? settings = null)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = PostgresFixture.CreateDbContext(connectionString);
        // A product nobody has reviewed yet, with stock for several buyers.
        var product = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.RatingCount == 0 && p.StockQuantity >= 10)
            .OrderBy(p => p.Sku)
            .Select(p => new { p.Id, p.Slug, p.Name })
            .FirstAsync(Ct);
        return new TestApi(new ApiFactory(connectionString, settings), connectionString, product.Id, product.Slug, product.Name);
    }

    private sealed class TestApi(ApiFactory factory, string connectionString, Guid productId, string slug, string name) : IAsyncDisposable
    {
        public ApiFactory Factory { get; } = factory;
        public Guid ProductId { get; } = productId;
        public string Slug { get; } = slug;
        public string Name { get; } = name;

        public async Task<BrowserClient> CustomerAsync(string displayName = "Test Shopper")
        {
            var client = Factory.CreateBrowserClient();
            Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(BrowserClient.NewEmail(), displayName: displayName)).StatusCode);
            return client;
        }

        /// <summary>Buys one unit through the real cart and fake-gateway checkout; returns the order number.</summary>
        public async Task<string> BuyAsync(BrowserClient client)
        {
            (await client.Http.PutAsJsonAsync($"/api/cart/items/{ProductId}", new { quantity = 1 }, Ct)).EnsureSuccessStatusCode();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
            {
                Content = JsonContent.Create(new CheckoutRequest(
                    new ShippingInput("Test Shopper", "10 Example Street", null, "Leipzig", "04109", "Germany", "+49 000 000 000", "shopper@test.example"),
                    new PaymentInput(FakePaymentGateway.SuccessCard, "12/30", "123", "Test Shopper"), null)),
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var response = await client.Http.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!.Number;
        }

        public Task<HttpResponseMessage> PostReviewAsync(BrowserClient client, int rating, string title = "Strong and comfy", string body = "Held up well through heavy pulls this month.") =>
            client.Http.PostAsJsonAsync($"/api/catalog/products/{Slug}/reviews", new { rating, title, body }, Ct);

        public async Task<ProductReviewsResponse> ReviewsAsync(BrowserClient client) =>
            (await client.Http.GetFromJsonAsync<ProductReviewsResponse>($"/api/catalog/products/{Slug}/reviews", Json, Ct))!;

        public async Task<(decimal, int)> RatingAsync()
        {
            var row = await Db(db => db.Products.Where(p => p.Id == ProductId).Select(p => new { p.RatingAverage, p.RatingCount }).SingleAsync(Ct));
            return (row.RatingAverage, row.RatingCount);
        }

        public async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
        {
            await using var db = PostgresFixture.CreateDbContext(connectionString);
            return await action(db);
        }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
