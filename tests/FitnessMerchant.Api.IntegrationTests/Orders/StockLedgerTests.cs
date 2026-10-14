using FitnessMerchant.Api.Features.Orders;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.IntegrationTests.Orders;

/// <summary>
/// Forces the exact interleaving that oversells with a naive "read stock, then write" implementation:
/// transaction A takes the last unit and has not committed when transaction B tries to take it too.
/// </summary>
public class StockLedgerTests(PostgresFixture postgres)
{
    private const string Kettlebell = "competition-kettlebell-24-kg";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_competing_take_waits_for_the_first_and_then_finds_nothing_left()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        Guid productId;
        await using (var setup = PostgresFixture.CreateDbContext(connectionString))
        {
            productId = await setup.Products.Where(p => p.Slug == Kettlebell).Select(p => p.Id).SingleAsync(Ct);
            await setup.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 1), Ct);
        }

        await using var a = PostgresFixture.CreateDbContext(connectionString);
        await using var b = PostgresFixture.CreateDbContext(connectionString);
        await using var transactionA = await a.Database.BeginTransactionAsync(Ct);
        await using var transactionB = await b.Database.BeginTransactionAsync(Ct);

        Assert.True(await StockLedger.TryTakeAsync(a, productId, 1, DateTimeOffset.UtcNow, Ct));

        var takeB = StockLedger.TryTakeAsync(b, productId, 1, DateTimeOffset.UtcNow, Ct);
        await Task.Delay(500, Ct);
        Assert.False(takeB.IsCompleted, "B must wait on A's row lock, not read stale stock.");

        await transactionA.CommitAsync(Ct);

        Assert.False(await takeB);
        await transactionB.CommitAsync(Ct);
        await using var check = PostgresFixture.CreateDbContext(connectionString);
        Assert.Equal(0, await check.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync(Ct));
    }

    [Fact]
    public async Task If_the_first_rolls_back_the_competing_take_gets_the_unit()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        Guid productId;
        await using (var setup = PostgresFixture.CreateDbContext(connectionString))
        {
            productId = await setup.Products.Where(p => p.Slug == Kettlebell).Select(p => p.Id).SingleAsync(Ct);
            await setup.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 1), Ct);
        }

        await using var a = PostgresFixture.CreateDbContext(connectionString);
        await using var b = PostgresFixture.CreateDbContext(connectionString);
        await using var transactionA = await a.Database.BeginTransactionAsync(Ct);
        await using var transactionB = await b.Database.BeginTransactionAsync(Ct);

        Assert.True(await StockLedger.TryTakeAsync(a, productId, 1, DateTimeOffset.UtcNow, Ct));
        var takeB = StockLedger.TryTakeAsync(b, productId, 1, DateTimeOffset.UtcNow, Ct);
        await Task.Delay(300, Ct);
        await transactionA.RollbackAsync(Ct); // e.g. a later line in A's cart was sold out

        Assert.True(await takeB);
        await transactionB.CommitAsync(Ct);
    }
}
