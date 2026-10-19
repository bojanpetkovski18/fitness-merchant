using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitnessMerchant.Api.Features.Admin;
using FitnessMerchant.Api.Features.Meta;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Seed;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessMerchant.Api.IntegrationTests.Admin;

public class DemoResetTests(PostgresFixture postgres)
{
    private const string AdminEmail = "admin@fitness-merchant.example";
    private const string CustomerEmail = "customer@fitness-merchant.example";
    private const string AdminPassword = "Demo-Admin-Pass-1";
    private const string CustomerPassword = "Demo-Customer-Pass-1";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_demo_mode_the_reset_does_not_exist()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Http.PostAsync("/api/admin/demo/reset", null, Ct)).StatusCode);
        Assert.Equal(new MetaResponse(false, null), await admin.Http.GetFromJsonAsync<MetaResponse>("/api/meta", Ct));
    }

    [Fact]
    public async Task The_reset_restores_the_seed_and_keeps_the_demo_admin_signed_in()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var factory = await StartDemoAsync(connectionString);
        var seed = await CountsAsync(connectionString);
        using var admin = factory.CreateBrowserClient();
        (await admin.SignInAsync(AdminEmail, AdminPassword)).EnsureSuccessStatusCode();
        using var customer = factory.CreateBrowserClient();
        (await customer.SignInAsync(CustomerEmail, CustomerPassword)).EnsureSuccessStatusCode();

        // What visitors do to a demo: new accounts, catalog edits, a renamed demo account.
        using var visitor = factory.CreateBrowserClient();
        (await visitor.RegisterAsync(BrowserClient.NewEmail())).EnsureSuccessStatusCode();
        var category = await Db(connectionString, db => db.Categories.Select(c => c.Id).FirstAsync(Ct));
        var brand = await Db(connectionString, db => db.Brands.Select(b => b.Id).FirstAsync(Ct));
        (await admin.Http.PostAsJsonAsync("/api/admin/products", new
        {
            sku = "VISITOR-1", name = "Visitor Product", description = "Added during the demo.", categoryId = category, brandId = brand,
            price = 9.99m, discountPercent = 0, stockQuantity = 1, isActive = true,
        }, Ct)).EnsureSuccessStatusCode();
        (await customer.Http.PutAsJsonAsync("/api/account/profile", new { displayName = "Renamed By Visitor" }, Ct)).EnsureSuccessStatusCode();
        Assert.NotEqual(seed, await CountsAsync(connectionString));

        var response = await admin.Http.PostAsync("/api/admin/demo/reset", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<DemoResetResponse>(Ct))!;
        Assert.Equal((seed.Products, seed.Orders, seed.Reviews), (result.Products, result.Orders, result.Reviews));
        Assert.Equal(seed, await CountsAsync(connectionString));
        Assert.Equal(0, await Db(connectionString, db => db.Products.CountAsync(p => p.Sku == "VISITOR-1", Ct)));
        Assert.Equal("FM-000001", await Db(connectionString, db => db.Orders.OrderBy(o => o.Number).Select(o => o.Number).FirstAsync(Ct)));
        // The demo accounts survive with their seed names; the visitor's account is gone.
        Assert.Equal("Demo Admin", (await admin.MeAsync()).User?.DisplayName);
        Assert.Equal("Demo Customer", (await customer.MeAsync()).User?.DisplayName);
        Assert.Null((await visitor.MeAsync()).User);
    }

    [Fact]
    public async Task Demo_accounts_cannot_change_their_password_but_other_accounts_can()
    {
        await using var factory = await StartDemoAsync(await postgres.CreateDatabaseAsync());
        using var customer = factory.CreateBrowserClient();
        (await customer.SignInAsync(CustomerEmail, CustomerPassword)).EnsureSuccessStatusCode();
        using var visitor = factory.CreateBrowserClient();
        (await visitor.RegisterAsync(BrowserClient.NewEmail())).EnsureSuccessStatusCode();

        var demo = await customer.Http.PostAsJsonAsync("/api/account/password", new { currentPassword = CustomerPassword, newPassword = "Hijacked-Pass-9" }, Ct);
        var own = await visitor.Http.PostAsJsonAsync("/api/account/password", new { currentPassword = BrowserClient.Password, newPassword = "Brand-New-Pass-7" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, demo.StatusCode);
        Assert.Equal("demo-account", ((JsonElement)(await demo.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Extensions["code"]!).GetString());
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
        using var again = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await again.SignInAsync(CustomerEmail, CustomerPassword)).StatusCode);
    }

    [Fact]
    public async Task Meta_tells_the_spa_about_demo_mode()
    {
        await using var factory = await StartDemoAsync(await postgres.CreateDatabaseAsync());
        using var guest = factory.CreateBrowserClient();

        Assert.Equal(new MetaResponse(true, 6), await guest.Http.GetFromJsonAsync<MetaResponse>("/api/meta", Ct));
    }

    [Fact]
    public async Task An_overdue_reset_runs_when_the_app_starts()
    {
        // A scale-to-zero host (Cloud Run) never keeps one process alive for the 6-hour interval, so the schedule
        // must come from the database: a new instance resets at once when the last reset is older than that.
        var connectionString = await postgres.CreateDatabaseAsync();
        await AddVisitorProductAsync(connectionString);
        await SetLastResetAsync(connectionString, DateTimeOffset.UtcNow.AddHours(-7));

        // The overdue reset seeds the demo accounts itself, as soon as the app starts; seeding them here as well would
        // race it for the same rows.
        await using var factory = await StartDemoAsync(connectionString, seedDemoUsers: false);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (await VisitorProductsAsync(connectionString) > 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(200, Ct);
        }
        Assert.Equal(0, await VisitorProductsAsync(connectionString));
        Assert.True(await Db(connectionString, db => db.DemoState.SingleAsync(Ct)) is { LastResetAt: var at } && at > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task A_reset_that_is_not_due_leaves_the_data_alone()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var factory = await StartDemoAsync(connectionString);
        await AddVisitorProductAsync(connectionString);
        await SetLastResetAsync(connectionString, DateTimeOffset.UtcNow.AddHours(-5));

        using var scope = factory.Services.CreateScope();
        var reset = await scope.ServiceProvider.GetRequiredService<DemoResetService>().ResetIfOverdueAsync(TimeSpan.FromHours(6), Ct);

        Assert.False(reset);
        Assert.Equal(1, await VisitorProductsAsync(connectionString));
    }

    [Fact]
    public async Task The_first_check_on_a_fresh_database_records_the_seed_instead_of_resetting()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var factory = await StartDemoAsync(connectionString);
        await Db(connectionString, db => db.DemoState.ExecuteDeleteAsync(Ct));
        await AddVisitorProductAsync(connectionString);

        using var scope = factory.Services.CreateScope();
        var reset = await scope.ServiceProvider.GetRequiredService<DemoResetService>().ResetIfOverdueAsync(TimeSpan.FromHours(6), Ct);

        Assert.False(reset);
        Assert.Equal(1, await VisitorProductsAsync(connectionString));
        Assert.Equal(1, await Db(connectionString, db => db.DemoState.CountAsync(Ct)));
    }

    // --- helpers ---

    private static async Task AddVisitorProductAsync(string connectionString)
    {
        await using var db = PostgresFixture.CreateDbContext(connectionString);
        var category = await db.Categories.FirstAsync(Ct);
        var brand = await db.Brands.FirstAsync(Ct);
        db.Products.Add(TestCatalog.Product(category, brand, "visitor-product", sku: "VISITOR-1"));
        await db.SaveChangesAsync(Ct);
    }

    private static Task<int> VisitorProductsAsync(string connectionString) =>
        Db(connectionString, db => db.Products.CountAsync(p => p.Sku == "VISITOR-1", Ct));

    private static Task<int> SetLastResetAsync(string connectionString, DateTimeOffset at) =>
        Db(connectionString, db => db.Database.ExecuteSqlAsync(
            $"INSERT INTO demo_state (id, last_reset_at) VALUES (1, {at}) ON CONFLICT (id) DO UPDATE SET last_reset_at = EXCLUDED.last_reset_at", Ct));

    private sealed record Counts(int Products, int Categories, int Brands, int Images, int Orders, int Reviews, int Users);

    private static async Task<ApiFactory> StartDemoAsync(string connectionString, bool seedDemoUsers = true)
    {
        var factory = new ApiFactory(connectionString, new Dictionary<string, string?>
        {
            ["DEMO_MODE"] = "true",
            ["DEMO_ADMIN_EMAIL"] = AdminEmail,
            ["DEMO_ADMIN_PASSWORD"] = AdminPassword,
            ["DEMO_CUSTOMER_EMAIL"] = CustomerEmail,
            ["DEMO_CUSTOMER_PASSWORD"] = CustomerPassword,
        });
        using var scope = factory.Services.CreateScope();
        if (seedDemoUsers)
        {
            await scope.ServiceProvider.GetRequiredService<DemoUserSeeder>().SeedAsync();
        }
        return factory;
    }

    private static Task<Counts> CountsAsync(string connectionString) => Db(connectionString, async db => new Counts(
        await db.Products.CountAsync(Ct), await db.Categories.CountAsync(Ct), await db.Brands.CountAsync(Ct), await db.Images.CountAsync(Ct),
        await db.Orders.CountAsync(Ct), await db.Reviews.CountAsync(Ct), await db.Users.CountAsync(Ct)));

    private static async Task<T> Db<T>(string connectionString, Func<AppDbContext, Task<T>> action)
    {
        await using var db = PostgresFixture.CreateDbContext(connectionString);
        return await action(db);
    }
}
