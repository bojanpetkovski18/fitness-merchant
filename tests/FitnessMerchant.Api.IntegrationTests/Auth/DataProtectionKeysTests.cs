using System.Net;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.IntegrationTests.Auth;

/// <summary>D032: the keys that sign session and antiforgery cookies live in the database, not in the container.</summary>
public class DataProtectionKeysTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_session_survives_a_restart_and_works_on_another_instance()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var cookies = new CookieContainer();
        var email = BrowserClient.NewEmail();
        await using (var first = new ApiFactory(connectionString))
        {
            var xsrf = new XsrfHeaderHandler(cookies);
            using var client = new BrowserClient(
                first.CreateDefaultClient(BrowserClient.BaseAddress, new CookieContainerHandler(cookies), xsrf), cookies, xsrf);
            (await client.RegisterAsync(email)).EnsureSuccessStatusCode();
        }

        await using (var db = PostgresFixture.CreateDbContext(connectionString))
        {
            var keys = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM data_protection_keys").SingleAsync(Ct);
            Assert.True(keys > 0);
        }

        // A new app instance, with the same database and the cookies from the first one.
        await using var second = new ApiFactory(connectionString);
        var secondXsrf = new XsrfHeaderHandler(cookies);
        using var again = new BrowserClient(
            second.CreateDefaultClient(BrowserClient.BaseAddress, new CookieContainerHandler(cookies), secondXsrf), cookies, secondXsrf);

        Assert.Equal(email, (await again.MeAsync()).User?.Email);
        Assert.Equal(HttpStatusCode.NoContent, (await again.SignOutAsync()).StatusCode);
    }
}
