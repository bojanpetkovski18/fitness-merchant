using System.Net;
using System.Net.Http.Json;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FitnessMerchant.Api.IntegrationTests;

public class ApiBasicsTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_returns_200_with_security_headers_when_the_database_is_reachable()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task Health_is_503_when_the_database_is_unreachable_but_liveness_stays_200()
    {
        // Port 1 on loopback refuses connections immediately.
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health", Ct);
        var live = await client.GetAsync("/health/live", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_is_401_for_guests_and_404_for_signed_in_users_both_as_problem_details()
    {
        // The fallback authorization policy also covers requests that match no endpoint, so guests cannot
        // probe which API routes exist (D021).
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();

        var asGuest = await browser.Http.GetAsync("/api/does-not-exist", Ct);
        await browser.RegisterAsync(BrowserClient.NewEmail());
        var asCustomer = await browser.Http.GetAsync("/api/does-not-exist", Ct);

        foreach (var (response, status) in new[] { (asGuest, 401), (asCustomer, 404) })
        {
            Assert.Equal(status, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
            Assert.Equal(status, problem!.Status);
            Assert.True(problem.Extensions.ContainsKey("traceId"));
        }
    }

    [Fact]
    public async Task Startup_fails_fast_without_a_connection_string()
    {
        await using var factory = new ApiFactory(connectionString: "");

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Database", error.Message, StringComparison.Ordinal);
    }
}
