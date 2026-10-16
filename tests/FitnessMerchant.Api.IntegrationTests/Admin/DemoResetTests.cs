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

    // --- helpers ---

    private sealed record Counts(int Products, int Categories, int Brands, int Images, int Orders, int Reviews, int Users);

    private static async Task<ApiFactory> StartDemoAsync(string connectionString)
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
        await scope.ServiceProvider.GetRequiredService<DemoUserSeeder>().SeedAsync();
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
