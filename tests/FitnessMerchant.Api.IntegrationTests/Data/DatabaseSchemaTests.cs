using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FitnessMerchant.Api.IntegrationTests.Data;

public class DatabaseSchemaTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_apply_to_an_empty_database()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateEmptyDatabaseAsync());

        await db.Database.MigrateAsync(Ct);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        var tables = await db.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync(Ct);
        Assert.Superset(new HashSet<string> { "categories", "brands", "products", "images" }, tables.ToHashSet());
    }

    [Fact]
    public async Task The_first_startup_on_an_empty_database_logs_no_errors()
    {
        // K3: EF used to log a failed query for the migrations history table before creating it.
        await using var factory = new ApiFactory(await postgres.CreateEmptyDatabaseAsync(), new Dictionary<string, string?>
        {
            ["Database:MigrateOnStartup"] = "true",
        });

        using var client = factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/health", Ct)).StatusCode);

        Assert.Contains(factory.Logs.Lines, l => l.Contains("Applying migration", StringComparison.Ordinal));
        var errors = factory.Logs.Lines.Where(l => l.StartsWith("Error ", StringComparison.Ordinal) || l.StartsWith("Critical ", StringComparison.Ordinal)).ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(e => e[..Math.Min(e.Length, 400)])));
    }

    [Fact]
    public async Task Model_matches_the_migrations()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());

        Assert.False(db.Database.HasPendingModelChanges(), "Run 'dotnet ef migrations add' for the model changes.");
    }

    [Fact]
    public async Task Product_slug_must_be_unique()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());
        var category = TestCatalog.Category();
        var brand = TestCatalog.Brand();
        db.Products.Add(TestCatalog.Product(category, brand, "same-slug", sku: "SKU-1"));
        db.Products.Add(TestCatalog.Product(category, brand, "same-slug", sku: "SKU-2"));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        var postgresError = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresError.SqlState);
        Assert.Equal("ix_products_slug", postgresError.ConstraintName);
    }

    [Fact]
    public async Task Product_sku_must_be_unique()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());
        var category = TestCatalog.Category();
        var brand = TestCatalog.Brand();
        db.Products.Add(TestCatalog.Product(category, brand, "first", sku: "SAME-SKU"));
        db.Products.Add(TestCatalog.Product(category, brand, "second", sku: "SAME-SKU"));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        var postgresError = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal("ix_products_sku", postgresError.ConstraintName);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("19.99")]
    [InlineData("1234567890.12")]
    public async Task Money_round_trips_exactly(string amount)
    {
        var price = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        var connectionString = await postgres.CreateDatabaseAsync();
        var category = TestCatalog.Category();
        var product = TestCatalog.Product(category, TestCatalog.Brand(), "money");
        product.Price = price;
        await using (var db = PostgresFixture.CreateDbContext(connectionString))
        {
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
        }

        await using var readDb = PostgresFixture.CreateDbContext(connectionString);
        var stored = await readDb.Products.Where(p => p.Id == product.Id).Select(p => p.Price).SingleAsync(Ct);

        Assert.Equal(price, stored);
    }

    [Theory]
    [InlineData(91)]
    [InlineData(-1)]
    public async Task Discount_outside_0_to_90_is_rejected_by_the_database(int discount)
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());
        var product = TestCatalog.Product(TestCatalog.Category(), TestCatalog.Brand(), "discount");
        product.DiscountPercent = discount;
        db.Products.Add(product);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        var postgresError = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgresError.SqlState);
        Assert.Equal("ck_products_discount_percent", postgresError.ConstraintName);
    }

    [Fact]
    public async Task Product_version_is_the_xmin_concurrency_token()
    {
        await using var db = PostgresFixture.CreateDbContext(await postgres.CreateDatabaseAsync());

        var version = db.Model.FindEntityType(typeof(Domain.Catalog.Product))!.FindProperty(nameof(Domain.Catalog.Product.Version))!;

        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
    }

    [Fact]
    public async Task Concurrent_product_updates_are_detected()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var product = TestCatalog.Product(TestCatalog.Category(), TestCatalog.Brand(), "concurrency");
        await using (var db = PostgresFixture.CreateDbContext(connectionString))
        {
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
        }

        await using var first = PostgresFixture.CreateDbContext(connectionString);
        await using var second = PostgresFixture.CreateDbContext(connectionString);
        var firstCopy = await first.Products.SingleAsync(p => p.Id == product.Id, Ct);
        var secondCopy = await second.Products.SingleAsync(p => p.Id == product.Id, Ct);

        firstCopy.StockQuantity = 1;
        await first.SaveChangesAsync(Ct);
        secondCopy.StockQuantity = 2;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }
}
