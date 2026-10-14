using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Features.Orders;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Payments;
using FitnessMerchant.Api.Infrastructure.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitnessMerchant.Api.IntegrationTests.Orders;

public class CheckoutTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    private const string Whey = "whey-isolate-vanilla-2-kg";        // 53.91, stock 42
    private const string Creatine = "creatine-monohydrate-500-g";    // 24.90, stock 80
    private const string Kettlebell = "competition-kettlebell-24-kg"; // 80.67, stock 3

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Paid_order_takes_stock_empties_the_cart_and_snapshots_prices()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 2), (Creatine, 1));

        var response = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Matches("^FM-[0-9]{6}$", order.Number);
        Assert.Equal($"/api/orders/{order.Number}", response.Headers.Location?.ToString());
        Assert.Equal((2 * 53.91m + 24.90m, 0m, 132.72m), (order.Subtotal, order.Shipping, order.Total));
        Assert.Equal(("Visa", "4242"), (order.Payment.CardBrand, order.Payment.CardLast4));
        Assert.Equal([OrderStatus.PendingPayment, OrderStatus.Paid], order.History.Select(h => h.Status));
        Assert.DoesNotContain(order.History, h => h.At > DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal((40, 79), (await api.StockAsync(Whey), await api.StockAsync(Creatine)));
        Assert.Equal(0, await api.Db(db => db.CartItems.CountAsync(Ct)));
        var item = order.Items.Single(i => i.ProductSlug == Whey);
        Assert.Equal((53.91m, 2, 107.82m, "HAL-0001"), (item.UnitPrice, item.Quantity, item.LineTotal, item.Sku));
    }

    [Fact]
    public async Task Declined_card_cancels_the_order_restores_stock_and_keeps_the_cart()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 2), (Creatine, 1));

        var response = await CheckoutAsync(customer, FakePaymentGateway.DeclineCard);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!;
        Assert.Equal("payment-declined", ((JsonElement)problem.Extensions["code"]!).GetString());
        Assert.Equal((42, 80), (await api.StockAsync(Whey), await api.StockAsync(Creatine)));
        var cart = await api.Db(db => db.CartItems.OrderBy(i => i.Quantity).Select(i => i.Quantity).ToListAsync(Ct));
        Assert.Equal([1, 2], cart);
        var order = await api.Db(db => db.Orders.Include(o => o.StatusChanges).SingleAsync(o => o.IdempotencyKey == customer.LastKey, Ct));
        Assert.Equal((OrderStatus.Cancelled, "payment-declined"), (order.Status, order.CancellationReason));
        Assert.Equal(
            [OrderStatus.PendingPayment, OrderStatus.Cancelled],
            order.StatusChanges.OrderBy(c => c.ChangedAt).ThenBy(c => c.Id).Select(c => c.ToStatus));

        // The kept cart can then be paid with another card.
        Assert.Equal(HttpStatusCode.Created, (await CheckoutAsync(customer, FakePaymentGateway.SuccessCard)).StatusCode);
    }

    [Fact]
    public async Task Empty_cart_is_a_400()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync();

        var response = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "cart-empty");
    }

    [Fact]
    public async Task A_price_change_after_review_is_a_409_and_the_order_uses_the_current_price()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1));
        await api.Db(db => db.Products.Where(p => p.Slug == Whey)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 70m).SetProperty(p => p.DiscountPercent, 0), Ct));

        var stale = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard, expectedTotal: 53.91m);
        var problem = await AssertProblemAsync(stale, HttpStatusCode.Conflict, "price-changed");
        Assert.Equal(70m, ((JsonElement)problem.Extensions["total"]!).GetDecimal());

        var fresh = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard, expectedTotal: 70m);
        var order = (await fresh.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
        Assert.Equal(70m, order.Items.Single().UnitPrice);
        Assert.Equal(42 - 1, await api.StockAsync(Whey));
    }

    [Fact]
    public async Task A_cart_with_an_archived_product_is_rejected_until_reviewed()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1), (Creatine, 1));
        await api.Db(db => db.Products.Where(p => p.Slug == Creatine).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), Ct));

        var response = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "cart-changed");
        Assert.Equal(42, await api.StockAsync(Whey)); // nothing was taken
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Six_customers_racing_for_the_last_unit_produce_exactly_one_order(int run)
    {
        _ = run; // repeated on purpose: a race that passes once can still be flaky
        await using var api = await StartAsync();
        var buyers = new List<Shopper>();
        for (var i = 0; i < 6; i++)
        {
            buyers.Add(await api.CustomerWithCartAsync((Kettlebell, 1)));
        }
        await api.Db(db => db.Products.Where(p => p.Slug == Kettlebell).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 1), Ct));

        var responses = await Task.WhenAll(buyers.Select(b => CheckoutAsync(b, FakePaymentGateway.SuccessCard)));
        buyers.ForEach(b => b.Dispose());

        // The losers see a stock error: either already at the cart check or at the atomic stock update.
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(0, await api.StockAsync(Kettlebell));
        Assert.Equal(1, await api.Db(db => db.Orders.CountAsync(o => o.Status == OrderStatus.Paid && o.Items.Any(i => i.ProductSlug == Kettlebell) && !o.IdempotencyKey.StartsWith("seed-"), Ct)));
    }

    [Fact]
    public async Task Repeating_a_submit_with_the_same_key_returns_the_same_order_once()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1));
        var key = $"key-{Guid.NewGuid():N}";

        var first = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard, key: key);
        var replay = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard, key: key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var a = (await first.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
        var b = (await replay.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
        Assert.Equal(a.Number, b.Number);
        Assert.Equal(41, await api.StockAsync(Whey)); // taken once
        Assert.Equal(1, await api.Db(db => db.Orders.CountAsync(o => o.IdempotencyKey == key, Ct)));
    }

    [Fact]
    public async Task Repeating_a_declined_submit_returns_the_decline_again()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1));
        var key = $"key-{Guid.NewGuid():N}";

        await CheckoutAsync(customer, FakePaymentGateway.DeclineCard, key: key);
        var replay = await CheckoutAsync(customer, FakePaymentGateway.SuccessCard, key: key);

        await AssertProblemAsync(replay, HttpStatusCode.PaymentRequired, "payment-declined");
    }

    [Fact]
    public async Task Missing_idempotency_key_is_a_400()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1));

        var response = await customer.Http.PostAsJsonAsync("/api/checkout", Request(FakePaymentGateway.SuccessCard), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_input_returns_field_errors_and_takes_nothing()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1));
        var request = Request("4242 4242 4242 4241") with
        {
            Shipping = ValidShipping with { City = "" },
            Payment = new PaymentInput("4242 4242 4242 4241", "01/20", "1", ""),
        };

        var response = await customer.Http.SendAsync(Post(request, $"key-{Guid.NewGuid():N}"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!;
        Assert.Equal(
            ["payment.cardNumber", "payment.cvc", "payment.expiry", "payment.nameOnCard", "shipping.city"],
            problem.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(42, await api.StockAsync(Whey));
    }

    [Fact]
    public async Task Cancelling_restocks_and_only_works_before_shipping()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 2));
        var order = (await (await CheckoutAsync(customer, FakePaymentGateway.SuccessCard)).Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
        Assert.True(order.CanCancel);

        var cancelled = await customer.Http.PostAsync($"/api/orders/{order.Number}/cancel", null, Ct);
        var again = await customer.Http.PostAsync($"/api/orders/{order.Number}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var body = (await cancelled.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
        Assert.Equal((OrderStatus.Cancelled, false, "customer"), (body.Status, body.CanCancel, body.CancellationReason));
        Assert.Equal(42, await api.StockAsync(Whey));
        await AssertProblemAsync(again, HttpStatusCode.Conflict, "not-cancellable");
        Assert.Equal(42, await api.StockAsync(Whey)); // not restocked twice
    }

    [Fact]
    public async Task A_shipped_order_cannot_be_cancelled()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1));
        var order = (await (await CheckoutAsync(customer, FakePaymentGateway.SuccessCard)).Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
        await api.Db(db => db.Database.ExecuteSqlAsync($"UPDATE orders SET status = 'Shipped' WHERE number = {order.Number}", Ct));

        var response = await customer.Http.PostAsync($"/api/orders/{order.Number}/cancel", null, Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "not-cancellable");
        Assert.Equal(41, await api.StockAsync(Whey));
    }

    [Fact]
    public async Task Customers_only_see_and_cancel_their_own_orders()
    {
        await using var api = await StartAsync();
        using var alice = await api.CustomerWithCartAsync((Whey, 1));
        using var bob = await api.CustomerWithCartAsync();
        var order = (await (await CheckoutAsync(alice, FakePaymentGateway.SuccessCard)).Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;

        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.GetAsync($"/api/orders/{order.Number}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.PostAsync($"/api/orders/{order.Number}/cancel", null, Ct)).StatusCode);
        Assert.Equal(0, (await bob.Http.GetFromJsonAsync<PagedResponse<OrderSummaryResponse>>("/api/orders", Json, Ct))!.TotalCount);

        var mine = (await alice.Http.GetFromJsonAsync<PagedResponse<OrderSummaryResponse>>("/api/orders", Json, Ct))!;
        Assert.Equal(order.Number, Assert.Single(mine.Items).Number);
        Assert.Equal(OrderStatus.Paid, (await alice.Http.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Number}", Json, Ct))!.Status);
    }

    [Fact]
    public async Task Card_numbers_never_appear_in_the_logs()
    {
        await using var api = await StartAsync();
        using var customer = await api.CustomerWithCartAsync((Whey, 1));

        await CheckoutAsync(customer, FakePaymentGateway.DeclineCard);
        await CheckoutAsync(customer, FakePaymentGateway.SuccessCard);

        Assert.NotEmpty(api.Factory.Logs.Lines);
        Assert.Contains(api.Factory.Logs.Lines, l => l.Contains("placed and paid", StringComparison.Ordinal));
        Assert.DoesNotContain(api.Factory.Logs.Lines, l =>
            l.Contains("4242424242424242", StringComparison.Ordinal) || l.Contains("4242 4242", StringComparison.Ordinal) ||
            l.Contains("4000000000000002", StringComparison.Ordinal) || l.Contains("4000 0000", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seeded_history_is_numbered_in_date_order_and_seeding_again_is_a_no_op()
    {
        await using var api = await StartAsync();

        var orders = await api.Db(db => db.Orders.AsNoTracking().OrderBy(o => o.Number).Select(o => new { o.Number, o.PlacedAt, o.Status }).ToListAsync(Ct));
        var seededAgain = await api.Db(db => new OrderSeeder(db, TimeProvider.System, NullLogger<OrderSeeder>.Instance).SeedAsync(Ct));

        Assert.Equal(OrderSeeder.OrderCount, orders.Count);
        Assert.Equal("FM-000001", orders[0].Number);
        Assert.Equal(orders.Select(o => o.PlacedAt).Order(), orders.Select(o => o.PlacedAt));
        Assert.True(orders.Select(o => o.Status).Distinct().Count() >= 3, "a mix of statuses for the dashboard");
        Assert.False(seededAgain);
        Assert.Equal(0, await api.Db(db => db.Users.CountAsync(u => u.Email!.EndsWith("@customers.example") && u.PasswordHash != null, Ct)));
    }

    // --- helpers ---

    private static readonly ShippingInput ValidShipping =
        new("Test Shopper", "10 Example Street", null, "Leipzig", "04109", "Germany", "+49 000 000 000", "shopper@test.example");

    private static CheckoutRequest Request(string card, decimal? expectedTotal = null) =>
        new(ValidShipping, new PaymentInput(card, "12/30", "123", "Test Shopper"), expectedTotal);

    private static HttpRequestMessage Post(CheckoutRequest request, string key)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/checkout") { Content = JsonContent.Create(request) };
        message.Headers.Add("Idempotency-Key", key);
        return message;
    }

    private static Task<HttpResponseMessage> CheckoutAsync(Shopper shopper, string card, decimal? expectedTotal = null, string? key = null)
    {
        shopper.LastKey = key ?? $"key-{Guid.NewGuid():N}";
        return shopper.Http.SendAsync(Post(Request(card, expectedTotal), shopper.LastKey), Ct);
    }

    private static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!;
        Assert.Equal(code, ((JsonElement)problem.Extensions["code"]!).GetString());
        return problem;
    }

    private async Task<TestApi> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = PostgresFixture.CreateDbContext(connectionString);
        var ids = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Id, Ct);
        return new TestApi(new ApiFactory(connectionString), connectionString, ids);
    }

    private sealed class Shopper(BrowserClient client) : IDisposable
    {
        public HttpClient Http => client.Http;
        public string LastKey { get; set; } = "";

        public void Dispose() => client.Dispose();
    }

    private sealed class TestApi(ApiFactory factory, string connectionString, Dictionary<string, Guid> ids) : IAsyncDisposable
    {
        public ApiFactory Factory { get; } = factory;

        public async Task<Shopper> CustomerWithCartAsync(params (string Slug, int Quantity)[] lines)
        {
            var client = Factory.CreateBrowserClient();
            Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(BrowserClient.NewEmail())).StatusCode);
            foreach (var (slug, quantity) in lines)
            {
                var put = await client.Http.PutAsJsonAsync($"/api/cart/items/{ids[slug]}", new { quantity }, Ct);
                Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            }
            return new Shopper(client);
        }

        public Task<int> StockAsync(string slug) => Db(db => db.Products.Where(p => p.Slug == slug).Select(p => p.StockQuantity).SingleAsync(Ct));

        public async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
        {
            await using var db = PostgresFixture.CreateDbContext(connectionString);
            return await action(db);
        }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
