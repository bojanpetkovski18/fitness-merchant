using System.Net;
using System.Net.Http.Json;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;

namespace FitnessMerchant.Api.IntegrationTests.Common;

/// <summary>K5: behind a trusted proxy the rate limits count each client, not the proxy.</summary>
public class ForwardedHeadersTests(PostgresFixture postgres)
{
    private const string Proxy = "10.0.0.5";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Behind_a_trusted_proxy_each_client_gets_its_own_sign_in_budget()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["Test:RemoteIp"] = Proxy,
            ["ReverseProxy:KnownProxies"] = Proxy,
            ["RateLimiting:AuthPermitLimit"] = "1",
        });
        using var browser = factory.CreateBrowserClient();

        var aliceFirst = await SignInFromAsync(browser, "203.0.113.10");
        var aliceSecond = await SignInFromAsync(browser, "203.0.113.10");
        var bobFirst = await SignInFromAsync(browser, "203.0.113.20");

        Assert.Equal(HttpStatusCode.Unauthorized, aliceFirst.StatusCode); // a real attempt (unknown user)
        Assert.Equal(HttpStatusCode.TooManyRequests, aliceSecond.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bobFirst.StatusCode); // not limited by Alice's attempts
    }

    [Fact]
    public async Task Without_a_trusted_proxy_forwarded_headers_are_ignored()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["Test:RemoteIp"] = Proxy,
            ["RateLimiting:AuthPermitLimit"] = "1",
        });
        using var browser = factory.CreateBrowserClient();

        await SignInFromAsync(browser, "203.0.113.10");
        // A client cannot claim a fresh address to get a fresh budget.
        var spoofed = await SignInFromAsync(browser, "203.0.113.99");

        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
    }

    [Fact]
    public async Task A_front_end_that_appends_its_own_address_needs_a_forward_limit_of_two()
    {
        // D034: some platforms (possibly Cloud Run, checked on the deployment) forward "client, front-end". With two
        // trusted hops each client gets its own budget, and whatever a client puts in front is still ignored.
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["Test:RemoteIp"] = Proxy,
            ["ReverseProxy:KnownNetworks"] = "10.0.0.0/8",
            ["ReverseProxy:ForwardLimit"] = "2",
            ["RateLimiting:AuthPermitLimit"] = "1",
        });
        using var browser = factory.CreateBrowserClient();

        var aliceFirst = await SignInFromAsync(browser, "198.51.100.1, 203.0.113.10, 10.0.0.9");
        var aliceSpoofing = await SignInFromAsync(browser, "198.51.100.2, 203.0.113.10, 10.0.0.9");
        var bobFirst = await SignInFromAsync(browser, "203.0.113.20, 10.0.0.9");

        Assert.Equal(HttpStatusCode.Unauthorized, aliceFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, aliceSpoofing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bobFirst.StatusCode);
    }

    private static async Task<HttpResponseMessage> SignInFromAsync(BrowserClient browser, string clientIp)
    {
        await browser.MeAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { email = "nobody@test.example", password = BrowserClient.Password }),
        };
        request.Headers.Add("X-Forwarded-For", clientIp);
        return await browser.Http.SendAsync(request, Ct);
    }
}
