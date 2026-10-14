using System.Net;
using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FitnessMerchant.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API in the "Testing" environment, pointed at a database chosen by the test.
/// Migrations and seeding at startup are off; the database comes prepared from <see cref="PostgresFixture"/>.
/// The auth rate limit is raised so only the test that targets it can hit it.
/// </summary>
public sealed class ApiFactory(string connectionString, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    /// <summary>Everything the API logged, for assertions about what must never appear (card numbers, passwords).</summary>
    public CapturedLogs Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseSetup.ConnectionStringName}", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services => services.AddSingleton<IEndpointMapper, TestEndpoints>());
        builder.ConfigureLogging(logging => logging.AddProvider(Logs).SetMinimumLevel(LogLevel.Debug));
    }

    /// <summary>A browser-like client: HTTPS, keeps cookies and echoes the XSRF cookie as a header (like Angular).</summary>
    public BrowserClient CreateBrowserClient()
    {
        var cookies = new CookieContainer();
        var xsrf = new XsrfHeaderHandler(cookies);
        var http = CreateDefaultClient(BrowserClient.BaseAddress, new CookieContainerHandler(cookies), xsrf);
        return new BrowserClient(http, cookies, xsrf);
    }

    private sealed class TestEndpoints : IEndpointMapper
    {
        public void Map(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/test/admin-only", () => Results.Ok("admin")).RequireAuthorization(AuthSetup.AdminPolicy);
            app.MapGet("/api/test/unannotated", () => Results.Ok("signed-in"));
        }
    }
}

public sealed class XsrfHeaderHandler(CookieContainer cookies) : DelegatingHandler
{
    public bool Enabled { get; set; } = true;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var unsafeMethod = request.Method != HttpMethod.Get && request.Method != HttpMethod.Head;
        var token = cookies.GetCookies(BrowserClient.BaseAddress)["XSRF-TOKEN"]?.Value;
        if (Enabled && unsafeMethod && token is not null)
        {
            request.Headers.Add("X-XSRF-TOKEN", token);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
