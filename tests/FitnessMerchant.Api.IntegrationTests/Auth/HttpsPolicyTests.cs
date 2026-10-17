using System.Net;
using System.Net.Http.Json;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;

namespace FitnessMerchant.Api.IntegrationTests.Auth;

/// <summary>D032: cookies are HTTPS-only outside Development unless Auth:RequireHttps is turned off (local compose).</summary>
public class HttpsPolicyTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task By_default_a_plain_http_session_request_is_refused()
    {
        // K13: a deployment must reach the API over HTTPS (or through a trusted proxy that says so).
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost") });

        var response = await client.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task With_RequireHttps_off_the_app_works_over_plain_http_without_secure_cookies()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["Auth:RequireHttps"] = "false",
        });
        var cookies = new CookieContainer();
        using var client = factory.CreateDefaultClient(new Uri("http://localhost"), new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies));

        var me = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.All(me.Headers.GetValues("Set-Cookie"), c => Assert.DoesNotContain("secure", c, StringComparison.OrdinalIgnoreCase));

        var xsrf = cookies.GetCookies(new Uri("http://localhost"))["XSRF-TOKEN"]!.Value;
        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { email = BrowserClient.NewEmail(), displayName = "Http Shopper", password = BrowserClient.Password }),
        };
        register.Headers.Add("X-XSRF-TOKEN", xsrf);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(register, Ct)).StatusCode);
    }
}
