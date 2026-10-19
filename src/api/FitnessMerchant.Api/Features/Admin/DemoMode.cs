using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Data.Configurations;
using FitnessMerchant.Api.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

/// <summary>When the demo was last reset (one row), so the schedule survives restarts and scale-to-zero.</summary>
public sealed class DemoState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public DateTimeOffset LastResetAt { get; set; }
}

internal sealed class DemoStateConfiguration : IEntityTypeConfiguration<DemoState>
{
    public void Configure(EntityTypeBuilder<DemoState> builder)
    {
        builder.ToTable("demo_state", t => t.HasCheckConstraint("ck_demo_state_singleton", $"id = {DemoState.SingletonId}"));
        builder.Property(s => s.Id).ValueGeneratedNever();
    }
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
        var started = time.GetUtcNow();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await LockAsync(cancellationToken);
            await ResetDataAsync(started, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await ReportAsync(started, cancellationToken);
    }

    /// <summary>
    /// Resets when the last reset is at least <paramref name="interval"/> old. The first check on a fresh database
    /// records the seed as the last reset instead. Under the same lock as a manual reset, so instances that start
    /// together reset once.
    /// </summary>
    public async Task<bool> ResetIfOverdueAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await LockAsync(cancellationToken);
            var state = await db.DemoState.SingleOrDefaultAsync(cancellationToken);
            if (state is null)
            {
                db.DemoState.Add(new DemoState { LastResetAt = now });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
            if (now - state.LastResetAt < interval)
            {
                return false;
            }
            await ResetDataAsync(now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await ReportAsync(now, cancellationToken);
        return true;
    }

    private Task<int> LockAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);

    private async Task ResetDataAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var demoEmails = DemoUserSeeder.DemoEmails(configuration).ToArray();
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
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO demo_state (id, last_reset_at) VALUES ({DemoState.SingletonId}, {now}) ON CONFLICT (id) DO UPDATE SET last_reset_at = EXCLUDED.last_reset_at",
            cancellationToken);
    }

    private async Task<DemoResetResponse> ReportAsync(DateTimeOffset started, CancellationToken cancellationToken)
    {
        var result = new DemoResetResponse(started,
            await db.Products.CountAsync(cancellationToken), await db.Orders.CountAsync(cancellationToken), await db.Reviews.CountAsync(cancellationToken));
        var elapsedMs = (time.GetUtcNow() - started).TotalMilliseconds;
        LogReset(logger, result.Products, result.Orders, result.Reviews, elapsedMs);
        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data reset: {Products} products, {Orders} orders, {Reviews} reviews in {ElapsedMs:0} ms")]
    private static partial void LogReset(ILogger logger, int products, int orders, int reviews, double elapsedMs);
}

/// <summary>
/// Resets the demo once the last reset (stored in the database) is <see cref="DemoOptions.ResetIntervalHours"/> old.
/// It checks at startup and then every few minutes, because a scale-to-zero host (Cloud Run) starts instances on
/// demand and never keeps one alive for hours, so an in-process timer alone would never fire.
/// </summary>
public sealed partial class DemoResetWorker(IServiceScopeFactory scopes, DemoOptions options, TimeProvider time, ILogger<DemoResetWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled || options.ResetIntervalHours == 0)
        {
            return;
        }

        var interval = TimeSpan.FromHours(options.ResetIntervalHours);
        using var timer = new PeriodicTimer(CheckInterval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DemoResetService>().ResetIfOverdueAsync(interval, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Keep the schedule alive; the next check tries again.
                LogFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled demo reset failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
