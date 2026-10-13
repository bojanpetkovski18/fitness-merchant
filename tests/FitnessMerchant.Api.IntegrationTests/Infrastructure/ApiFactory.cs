using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FitnessMerchant.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API in the "Testing" environment, pointed at a database chosen by the test.
/// Migrations and seeding at startup are off; the database comes prepared from <see cref="PostgresFixture"/>.
/// </summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseSetup.ConnectionStringName}", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Seed:Enabled", "false");
    }
}
