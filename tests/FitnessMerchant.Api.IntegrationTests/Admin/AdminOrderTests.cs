using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Features.Admin;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.IntegrationTests.Admin;

public class AdminOrderTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_status_pair_is_allowed_or_refused_exactly_as_the_state_machine_says()
    {
        await using var api = await StartAsync();
        var statuses = Enum.GetValues<OrderStatus>();
        var pairs = statuses.SelectMany(from => statuses.Select(to => (from, to))).ToList();
        // One seeded order per pair, put into the "from" status directly.
        var numbers = await api.Db(db => db.Orders.OrderBy(o => o.Number).Select(o => o.Number).Take(pairs.Count).ToListAsync(Ct));
        Assert.Equal(25, pairs.Count);

        var failures = new List<string>();
        foreach (var ((from, to), number) in pairs.Zip(numbers))
        {
            await api.Db(db => db.Database.ExecuteSqlAsync($"UPDATE orders SET status = {from.ToString()} WHERE number = {number}", Ct));
            var response = await api.Admin.Http.PostAsJsonAsync($"/api/admin/orders/{number}/status", new { toStatus = Kebab(to), note = "test" }, Ct);
            var expected = OrderStateMachine.CanTransition(from, to) ? HttpStatusCode.OK : HttpStatusCode.Conflict;
            if (response.StatusCode != expected)
            {
                failures.Add($"{from} -> {to}: {response.StatusCode}, expected {expected}");
            }
            else if (expected == HttpStatusCode.Conflict)
            {
                var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!;
                Assert.Equal("invalid-transition", ((JsonElement)problem.Extensions["code"]!).GetString());
            }
        }
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Shipping_records_the_admin_the_note_and_the_time_and_returns_the_next_moves()
    {
        await using var api = await StartAsync();
        var number = await api.Db(db => db.Orders.Where(o => o.Status == OrderStatus.Paid).Select(o => o.Number).FirstAsync(Ct));

        var response = await api.Admin.Http.PostAsJsonAsync($"/api/admin/orders/{number}/status", new { toStatus = "shipped", note = "  Tracking FM-TRACK-1  " }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<AdminOrderResponse>(Json, Ct))!;
        Assert.Equal(OrderStatus.Shipped, order.Order.Status);
        Assert.Equal([OrderStatus.Delivered], order.AllowedTransitions);
        var last = order.History[^1];
        Assert.Equal((OrderStatus.Paid, OrderStatus.Shipped, "Tracking FM-TRACK-1", "Test Shopper"), (last.FromStatus, last.ToStatus, last.Note, last.ChangedBy));
        Assert.NotEqual(Guid.Empty, order.Customer.Id);
        Assert.NotNull(await api.Db(db => db.Orders.Where(o => o.Number == number).Select(o => o.ShippedAt).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Cancelling_restocks_every_item()
    {
        await using var api = await StartAsync();
        var order = await api.Db(db => db.Orders.Include(o => o.Items).Where(o => o.Status == OrderStatus.Paid).FirstAsync(Ct));
        var ids = order.Items.Select(i => i.ProductId).ToList();
        var before = await api.Db(db => db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.StockQuantity, Ct));

        var response = await api.Admin.Http.PostAsJsonAsync($"/api/admin/orders/{order.Number}/status", new { toStatus = "cancelled" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await api.Db(db => db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.StockQuantity, Ct));
        foreach (var item in order.Items.GroupBy(i => i.ProductId))
        {
            Assert.Equal(before[item.Key] + item.Sum(i => i.Quantity), after[item.Key]);
        }
        Assert.Equal("admin", await api.Db(db => db.Orders.Where(o => o.Number == order.Number).Select(o => o.CancellationReason).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Status_change_input_is_validated()
    {
        await using var api = await StartAsync();
        var number = await api.Db(db => db.Orders.Select(o => o.Number).FirstAsync(Ct));

        var missing = await api.Admin.Http.PostAsJsonAsync($"/api/admin/orders/{number}/status", new { note = new string('x', 501) }, Ct);
        var unknown = await api.Admin.Http.PostAsJsonAsync("/api/admin/orders/FM-999999/status", new { toStatus = "shipped" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(["note", "toStatus"], (await missing.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Order_list_filters_by_status_dates_and_search()
    {
        await using var api = await StartAsync();
        await api.AddOrderAsync("FM-900001", new DateTimeOffset(2030, 3, 1, 10, 0, 0, TimeSpan.Zero), OrderStatus.Shipped, email: "findme@test.example");

        var byStatus = await api.Admin.Http.GetFromJsonAsync<PagedResponse<AdminOrderRow>>("/api/admin/orders?status=shipped", Json, Ct);
        var byDates = await api.Admin.Http.GetFromJsonAsync<PagedResponse<AdminOrderRow>>("/api/admin/orders?from=2030-03-01&to=2030-03-01", Json, Ct);
        var byEmail = await api.Admin.Http.GetFromJsonAsync<PagedResponse<AdminOrderRow>>("/api/admin/orders?q=FINDME", Json, Ct);
        var byNumber = await api.Admin.Http.GetFromJsonAsync<PagedResponse<AdminOrderRow>>("/api/admin/orders?q=900001", Json, Ct);
        var bad = await api.Admin.Http.GetAsync("/api/admin/orders?status=lost&from=2030-13-01", Ct);

        Assert.All(byStatus!.Items, o => Assert.Equal(OrderStatus.Shipped, o.Status));
        Assert.Equal("FM-900001", Assert.Single(byDates!.Items).Number);
        Assert.Equal("FM-900001", Assert.Single(byEmail!.Items).Number);
        Assert.Equal("FM-900001", Assert.Single(byNumber!.Items).Number);
        Assert.Equal(["from", "status"], (await bad.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Dashboard_on_a_known_dataset_counts_only_paid_orders_and_zero_fills_the_days()
    {
        await using var api = await StartAsync();
        // Far in the future, so the seeded history (last 90 days) cannot leak in.
        var day1 = new DateTimeOffset(2030, 3, 1, 9, 0, 0, TimeSpan.Zero);
        await api.AddOrderAsync("FM-900011", day1, OrderStatus.Paid, ("whey-isolate-vanilla-2-kg", 2, 50.00m));
        await api.AddOrderAsync("FM-900012", day1.AddHours(14).AddMinutes(59), OrderStatus.Delivered, ("creatine-monohydrate-500-g", 1, 20.00m)); // 23:59 UTC, still day 1
        await api.AddOrderAsync("FM-900013", day1.AddHours(3), OrderStatus.Cancelled, ("whey-isolate-vanilla-2-kg", 5, 50.00m));
        await api.AddOrderAsync("FM-900014", day1.AddHours(5), OrderStatus.PendingPayment, ("whey-isolate-vanilla-2-kg", 5, 50.00m));
        await api.AddOrderAsync("FM-900015", day1.AddDays(2), OrderStatus.Shipped, ("creatine-monohydrate-500-g", 3, 20.00m));

        var dashboard = (await api.Admin.Http.GetFromJsonAsync<DashboardResponse>("/api/admin/dashboard?from=2030-03-01&to=2030-03-04", Json, Ct))!;

        // 104.90 + 24.90 + 64.90: subtotals 100, 20 and 60, each plus 4.90 shipping; cancelled and unpaid ones excluded.
        Assert.Equal((194.70m, 3, 64.90m), (dashboard.Revenue, dashboard.OrderCount, dashboard.AverageOrderValue));
        Assert.Equal(
            [(new DateOnly(2030, 3, 1), 129.80m, 2), (new DateOnly(2030, 3, 2), 0m, 0), (new DateOnly(2030, 3, 3), 64.90m, 1), (new DateOnly(2030, 3, 4), 0m, 0)],
            dashboard.RevenueByDay.Select(d => (d.Day, d.Revenue, d.Orders)));
        Assert.Equal(
            [("Creatine Monohydrate 500 g", 4, 80.00m), ("Whey Isolate Vanilla 2 kg", 2, 100.00m)],
            dashboard.TopProducts.Select(t => (t.Name, t.Quantity, t.Revenue)));
    }

    [Fact]
    public async Task Dashboard_over_the_seeded_history_matches_hand_written_sql()
    {
        await using var api = await StartAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-99);
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var dashboard = (await api.Admin.Http.GetFromJsonAsync<DashboardResponse>($"/api/admin/dashboard?from={from:yyyy-MM-dd}&to={today:yyyy-MM-dd}", Json, Ct))!;

        // Written independently of the handler: plain SQL, grouped by the UTC calendar date.
        var totals = await api.Db(db => db.Database.SqlQuery<TotalsRow>($"""
            SELECT COALESCE(SUM(total), 0) AS revenue, COUNT(*)::int AS orders
            FROM orders WHERE status IN ('Paid', 'Shipped', 'Delivered') AND placed_at >= {start} AND placed_at < {end}
            """).SingleAsync(Ct));
        var perDay = await api.Db(db => db.Database.SqlQuery<PerDayRow>($"""
            SELECT (placed_at AT TIME ZONE 'UTC')::date AS day, SUM(total) AS revenue
            FROM orders WHERE status IN ('Paid', 'Shipped', 'Delivered') AND placed_at >= {start} AND placed_at < {end}
            GROUP BY 1
            """).ToDictionaryAsync(r => r.Day, r => r.Revenue, Ct));
        var top = await api.Db(db => db.Database.SqlQuery<TopRow>($"""
            SELECT p.name, SUM(i.quantity)::int AS quantity, SUM(i.line_total) AS revenue
            FROM order_items i JOIN orders o ON o.id = i.order_id JOIN products p ON p.id = i.product_id
            WHERE o.status IN ('Paid', 'Shipped', 'Delivered') AND o.placed_at >= {start} AND o.placed_at < {end}
            GROUP BY p.id, p.name ORDER BY 2 DESC, 3 DESC LIMIT 5
            """).ToListAsync(Ct));
        var lowStock = await api.Db(db => db.Database.SqlQuery<int>($"""SELECT COUNT(*)::int AS "Value" FROM products WHERE is_active AND stock_quantity <= 5""").SingleAsync(Ct));

        Assert.True(totals.Orders > 10, "the seed should give the dashboard real data");
        Assert.Equal((totals.Revenue, totals.Orders), (dashboard.Revenue, dashboard.OrderCount));
        Assert.Equal(Math.Round(totals.Revenue / totals.Orders, 2, MidpointRounding.AwayFromZero), dashboard.AverageOrderValue);
        Assert.Equal(100, dashboard.RevenueByDay.Count);
        Assert.All(dashboard.RevenueByDay, d => Assert.Equal(perDay.GetValueOrDefault(d.Day), d.Revenue));
        Assert.Equal(top.Select(t => (t.Quantity, t.Revenue)), dashboard.TopProducts.Select(t => (t.Quantity, t.Revenue)));
        Assert.Equal(Math.Min(lowStock, GetDashboardQuery.LowStockLimit), dashboard.LowStock.Count);
    }

    [Theory]
    [InlineData("2030-03-05", "2030-03-01", "from")]
    [InlineData("2030-01-01", "2031-01-02", "to")]   // 367 days
    [InlineData("yesterday", "2030-03-01", "from")]
    public async Task Dashboard_range_is_validated(string from, string to, string field)
    {
        await using var api = await StartAsync();

        var response = await api.Admin.Http.GetAsync($"/api/admin/dashboard?from={from}&to={to}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!.Errors.Keys);
    }

    [Fact]
    public async Task Csv_export_escapes_cells_and_neutralizes_formulas()
    {
        await using var api = await StartAsync();
        var day = new DateTimeOffset(2030, 4, 1, 8, 0, 0, TimeSpan.Zero);
        await api.AddOrderAsync("FM-900021", day, OrderStatus.Paid, name: "=HYPERLINK(\"http://evil.example\",\"x\")", city: "+1");
        await api.AddOrderAsync("FM-900022", day.AddMinutes(1), OrderStatus.Paid, name: "Ana \"The Lifter\", Jr.", city: "Line one\nLine two");
        await api.AddOrderAsync("FM-900023", day.AddMinutes(2), OrderStatus.Paid, name: "-1", city: "@SUM(A1:A9)");

        var response = await api.Admin.Http.GetAsync("/api/admin/orders/export?from=2030-04-01&to=2030-04-01", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("orders-2030-04-01-to-2030-04-01.csv", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n");

        Assert.Equal("order_number,placed_at_utc,status,customer_name,contact_email,country,city,items,subtotal,shipping,total,currency", lines[0]);
        Assert.Equal("FM-900021,2030-04-01 08:00:00,paid,\"'=HYPERLINK(\"\"http://evil.example\"\",\"\"x\"\")\",shopper@test.example,Germany,'+1,1,10.00,4.90,14.90,EUR", lines[1]);
        // Records end with CRLF; the LF inside the quoted city belongs to the cell.
        Assert.Equal("FM-900022,2030-04-01 08:01:00,paid,\"Ana \"\"The Lifter\"\", Jr.\",shopper@test.example,Germany,\"Line one\nLine two\",1,10.00,4.90,14.90,EUR", lines[2]);
        Assert.Equal("FM-900023,2030-04-01 08:02:00,paid,'-1,shopper@test.example,Germany,'@SUM(A1:A9),1,10.00,4.90,14.90,EUR", lines[3]);
        Assert.Equal("", lines[4]); // the file ends with a line break
    }

    [Fact]
    public async Task Csv_export_refuses_more_rows_than_the_limit_and_needs_a_range()
    {
        await using var api = await StartAsync(new Dictionary<string, string?> { ["Export:MaxRows"] = "3" });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var tooMany = await api.Admin.Http.GetAsync($"/api/admin/orders/export?from={today.AddDays(-99):yyyy-MM-dd}&to={today:yyyy-MM-dd}", Ct);
        var noRange = await api.Admin.Http.GetAsync("/api/admin/orders/export", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        var problem = (await tooMany.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!;
        Assert.Equal("too-many-rows", ((JsonElement)problem.Extensions["code"]!).GetString());
        Assert.Equal(3, ((JsonElement)problem.Extensions["maxRows"]!).GetInt32());
        Assert.Equal(["from", "to"], (await noRange.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Csv_export_is_rate_limited()
    {
        await using var api = await StartAsync(new Dictionary<string, string?> { ["RateLimiting:ExportPermitLimit"] = "2" });
        const string url = "/api/admin/orders/export?from=2030-01-01&to=2030-01-02";

        Assert.Equal(HttpStatusCode.OK, (await api.Admin.Http.GetAsync(url, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Admin.Http.GetAsync(url, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await api.Admin.Http.GetAsync(url, Ct)).StatusCode);
    }

    // --- helpers ---

    private static string Kebab(OrderStatus status) => JsonNamingPolicy.KebabCaseLower.ConvertName(status.ToString());

    private sealed record TotalsRow(decimal Revenue, int Orders);

    private sealed record PerDayRow(DateOnly Day, decimal Revenue);

    private sealed record TopRow(string Name, int Quantity, decimal Revenue);

    private async Task<TestApi> StartAsync(Dictionary<string, string?>? settings = null)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var factory = new ApiFactory(connectionString, settings);
        return new TestApi(factory, await factory.CreateAdminClientAsync(), connectionString);
    }

    private sealed class TestApi(ApiFactory factory, BrowserClient admin, string connectionString) : IAsyncDisposable
    {
        public BrowserClient Admin { get; } = admin;

        /// <summary>An order with fixed time, status and lines (10.00 × 1 of the first seeded product by default).</summary>
        public Task AddOrderAsync(string number, DateTimeOffset placedAt, OrderStatus status, params (string Slug, int Quantity, decimal UnitPrice)[] lines) =>
            AddOrderAsync(number, placedAt, status, "Test Shopper", "Leipzig", "shopper@test.example", lines);

        public Task AddOrderAsync(string number, DateTimeOffset placedAt, OrderStatus status, string name, string city) =>
            AddOrderAsync(number, placedAt, status, name, city, "shopper@test.example", []);

        public Task AddOrderAsync(string number, DateTimeOffset placedAt, OrderStatus status, string email) =>
            AddOrderAsync(number, placedAt, status, "Test Shopper", "Leipzig", email, []);

        private async Task AddOrderAsync(string number, DateTimeOffset placedAt, OrderStatus status, string name, string city, string email, (string Slug, int Quantity, decimal UnitPrice)[] lines) =>
            await Db(async db =>
            {
                var userId = await db.Users.Select(u => u.Id).FirstAsync(Ct);
                if (lines.Length == 0)
                {
                    lines = [("whey-isolate-vanilla-2-kg", 1, 10.00m)];
                }
                var products = await db.Products.Where(p => lines.Select(l => l.Slug).Contains(p.Slug)).ToDictionaryAsync(p => p.Slug, Ct);
                var orderId = Guid.CreateVersion7();
                var items = lines.Select(l => new OrderItem
                {
                    Id = Guid.CreateVersion7(),
                    OrderId = orderId,
                    ProductId = products[l.Slug].Id,
                    ProductName = products[l.Slug].Name,
                    ProductSlug = l.Slug,
                    Sku = products[l.Slug].Sku,
                    UnitPrice = l.UnitPrice,
                    Quantity = l.Quantity,
                    LineTotal = l.UnitPrice * l.Quantity,
                }).ToList();
                var order = Order.Place(orderId, number, userId, items, items.Sum(i => i.LineTotal), 4.90m,
                    new ShippingAddress(name, "1 Test Street", null, city, "04109", "Germany", "+49 000 000"), email, "Fake", $"key-{number}", placedAt);
                foreach (var step in PathTo(status))
                {
                    order.TransitionTo(step, placedAt, changedBy: null, note: null);
                }
                db.Orders.Add(order);
                return await db.SaveChangesAsync(Ct);
            });

        private static OrderStatus[] PathTo(OrderStatus status) => status switch
        {
            OrderStatus.PendingPayment => [],
            OrderStatus.Paid => [OrderStatus.Paid],
            OrderStatus.Shipped => [OrderStatus.Paid, OrderStatus.Shipped],
            OrderStatus.Delivered => [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered],
            _ => [OrderStatus.Cancelled],
        };

        public async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
        {
            await using var db = PostgresFixture.CreateDbContext(connectionString);
            return await action(db);
        }

        public async ValueTask DisposeAsync()
        {
            Admin.Dispose();
            await factory.DisposeAsync();
        }
    }
}
