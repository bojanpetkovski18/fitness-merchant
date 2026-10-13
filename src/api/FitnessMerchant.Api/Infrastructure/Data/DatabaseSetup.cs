using FitnessMerchant.Api.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FitnessMerchant.Api.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Apply pending migrations at startup (development and containers).</summary>
    public bool MigrateOnStartup { get; init; }
}

public static class DatabaseSetup
{
    public const string ConnectionStringName = "Database";

    public static WebApplicationBuilder AddAppDatabase(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail fast with a clear message instead of a connection error on the first request.
            throw new InvalidOperationException(
                $"Missing required setting 'ConnectionStrings:{ConnectionStringName}' " +
                $"(environment variable ConnectionStrings__{ConnectionStringName}). See .env.example.");
        }

        builder.Services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        builder.Services.AddOptions<DatabaseOptions>().Bind(builder.Configuration.GetSection(DatabaseOptions.Section));
        builder.Services.AddOptions<SeedOptions>().Bind(builder.Configuration.GetSection(SeedOptions.Section));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<CatalogSeeder>();
        builder.Services.AddScoped<DemoUserSeeder>();

        builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        return builder;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (options.MigrateOnStartup)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        if (scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value.Enabled)
        {
            await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
            await scope.ServiceProvider.GetRequiredService<DemoUserSeeder>().SeedAsync();
        }
    }
}
