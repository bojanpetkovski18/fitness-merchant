using System.Net;
using System.Text;
using FitnessMerchant.Api.Domain.Catalog;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitnessMerchant.Api.IntegrationTests.Data;

public class CatalogSeedTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeded_catalog_has_the_expected_shape()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());

        Assert.Equal(CatalogSeedData.Categories.Count, await db.Categories.CountAsync(Ct));
        Assert.Equal(CatalogSeedData.Brands.Count, await db.Brands.CountAsync(Ct));
        Assert.Equal(CatalogSeedData.Products.Count, await db.Products.CountAsync(Ct));

        var perDepartment = await db.Categories.GroupBy(c => c.Department).Select(g => g.Count()).ToListAsync(Ct);
        Assert.Equal([4, 4], perDepartment);

        Assert.False(await db.Products.AnyAsync(p => p.ImageId == null, Ct), "Every product has artwork.");
        Assert.False(await db.Brands.AnyAsync(b => b.LogoImageId == null, Ct), "Every brand has a logo.");
        Assert.True(await db.Products.AnyAsync(p => p.StockQuantity == 0, Ct), "Some products are out of stock.");
        Assert.True(await db.Products.AnyAsync(p => p.StockQuantity > 0 && p.StockQuantity <= 5, Ct), "Some products are low on stock.");
        Assert.True(await db.Products.AnyAsync(p => p.DiscountPercent > 0, Ct), "Some products are discounted.");
    }

    [Fact]
    public async Task Seeding_an_already_seeded_database_changes_nothing()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());
        var before = await CountsAsync(db);

        var seeded = await NewSeeder(db).SeedAsync(Ct);

        Assert.False(seeded);
        Assert.Equal(before, await CountsAsync(db));
    }

    [Fact]
    public async Task Seeding_a_fresh_database_twice_seeds_once_with_deterministic_ids()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateEmptyDatabaseAsync());
        await db.Database.MigrateAsync(Ct);

        Assert.True(await NewSeeder(db).SeedAsync(Ct));
        var afterFirst = await CountsAsync(db);
        Assert.False(await NewSeeder(db).SeedAsync(Ct));
        Assert.Equal(afterFirst, await CountsAsync(db));

        var product = await db.Products.SingleAsync(p => p.Slug == "whey-isolate-vanilla-2-kg", Ct);
        Assert.Equal(SeedIds.For("product:whey-isolate-vanilla-2-kg"), product.Id);
    }

    [Fact]
    public async Task Seed_skus_follow_the_brand_prefix_pattern()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());

        var skus = await db.Products.Select(p => p.Sku).ToListAsync(Ct);

        Assert.All(skus, sku => Assert.Matches("^(HAL|QUI|VER|BRK|TID|ORR)-[0-9]{4}$", sku));
    }

    [Fact]
    public async Task Seed_artwork_is_served_as_svg_with_security_headers()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        Guid imageId;
        await using (var db = PostgresFixture.CreateDbContext(connectionString))
        {
            imageId = await db.Products.Where(p => p.Slug == "competition-kettlebell-16-kg").Select(p => p.ImageId!.Value).SingleAsync(Ct);
        }
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/images/{imageId}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        var svg = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.Contains(">16<", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_image_returns_404_problem_details()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/images/{Guid.CreateVersion7()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static CatalogSeeder NewSeeder(AppDbContext db) =>
        new(db, TimeProvider.System, NullLogger<CatalogSeeder>.Instance);

    private static async Task<(int Categories, int Brands, int Products, int Images)> CountsAsync(AppDbContext db) => (
        await db.Categories.CountAsync(Ct),
        await db.Brands.CountAsync(Ct),
        await db.Products.CountAsync(Ct),
        await db.Images.CountAsync(Ct));
}
