using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Data.Configurations;
using FitnessMerchant.Api.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>Demo mode (SPEC F17) is for the public deployment only: <c>DEMO_MODE=true</c>.</summary>
public sealed class DemoOptions
{
    public bool Enabled { get; init; }

    /// <summary>How often the scheduled reset runs; 0 turns the schedule off (the admin button still works).</summary>
    public int ResetIntervalHours { get; init; } = 6;

    public static DemoOptions From(IConfiguration configuration) => new()
    {
        Enabled = configuration.GetValue("DEMO_MODE", false),
        ResetIntervalHours = Math.Max(0, configuration.GetValue("DEMO_RESET_INTERVAL_HOURS", 6)),
    };
}

public sealed record DemoResetResponse(DateTimeOffset ResetAt, int Products, int Orders, int Reviews);

/// <summary>
/// Puts the demo back to its seed state in one transaction (D030): everything people created (accounts, carts,
/// orders, reviews, catalog edits, uploads) is removed and the seeders run again. The demo accounts are kept, with
/// their names and roles restored, so an admin who presses the button stays signed in. An advisory lock makes a
/// manual and a scheduled reset (or two instances) run one after the other.
/// </summary>
public sealed partial class DemoResetService(
    AppDbContext db,
    CatalogSeeder catalog,
    DemoUserSeeder demoUsers,
    OrderSeeder orders,
    ReviewSeeder reviews,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<DemoResetService> logger)
{
    private const long AdvisoryLockKey = 0x46_4D_44_45_4D_4F; // "FMDEMO"

    public async Task<DemoResetResponse> ResetAsync(CancellationToken cancellationToken)
    {
        var demoEmails = DemoUserSeeder.DemoEmails(configuration).ToArray();
        var started = time.GetUtcNow();

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE reviews, order_status_changes, order_items, orders, cart_items, carts, products, brands, categories, images CASCADE",
                cancellationToken);
            // Every account except the demo ones; their carts, reviews and Identity rows go with them (cascade).
            await db.Users.Where(u => !demoEmails.Contains(u.NormalizedEmail!)).ExecuteDeleteAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync($"ALTER SEQUENCE {OrderConfiguration.NumberSequence} RESTART WITH 1", cancellationToken);
            db.ChangeTracker.Clear();

            await catalog.SeedAsync(cancellationToken);
            await demoUsers.SeedAsync(restore: true);
            await orders.SeedAsync(cancellationToken);
            await reviews.SeedAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var result = new DemoResetResponse(started,
            await db.Products.CountAsync(cancellationToken), await db.Orders.CountAsync(cancellationToken), await db.Reviews.CountAsync(cancellationToken));
        var elapsedMs = (time.GetUtcNow() - started).TotalMilliseconds;
        LogReset(logger, result.Products, result.Orders, result.Reviews, elapsedMs);
        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data reset: {Products} products, {Orders} orders, {Reviews} reviews in {ElapsedMs:0} ms")]
    private static partial void LogReset(ILogger logger, int products, int orders, int reviews, double elapsedMs);
}

/// <summary>Runs the reset every <see cref="DemoOptions.ResetIntervalHours"/> hours while demo mode is on.</summary>
public sealed partial class DemoResetWorker(IServiceScopeFactory scopes, DemoOptions options, TimeProvider time, ILogger<DemoResetWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled || options.ResetIntervalHours == 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.ResetIntervalHours), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DemoResetService>().ResetAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Keep the schedule alive; the next tick tries again.
                LogFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled demo reset failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
