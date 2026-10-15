using System.Net;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;

namespace FitnessMerchant.Api.IntegrationTests.Common;

/// <summary>The API serving the production SPA build (D029): files, deep links, caching, and API routes left alone.</summary>
public sealed class SpaHostingTests(PostgresFixture postgres) : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("fm-spa-").FullName;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deep_links_get_index_html_uncached_and_hashed_bundles_are_immutable()
    {
        await using var factory = await StartAsync();
        using var client = factory.CreateClient();

        var home = await client.GetAsync("/", Ct);
        var deepLink = await client.GetAsync("/products/whey-isolate-vanilla-2-kg", Ct);
        var bundle = await client.GetAsync("/main-I64NK2YK.js", Ct);
        var icon = await client.GetAsync("/favicon.svg", Ct);

        foreach (var page in new[] { home, deepLink })
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
            Assert.Contains("fm-test-index", await page.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
            Assert.Equal("no-cache", page.Headers.CacheControl?.ToString());
            Assert.Contains("script-src 'self'", page.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        }
        Assert.Equal(HttpStatusCode.OK, bundle.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", bundle.Headers.CacheControl?.ToString());
        Assert.Equal("public, max-age=3600", icon.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("/api/no-such-endpoint", HttpStatusCode.Unauthorized)]
    [InlineData("/api", HttpStatusCode.Unauthorized)]
    [InlineData("/health/nope", HttpStatusCode.Unauthorized)]
    [InlineData("/missing-file.png", HttpStatusCode.Unauthorized)] // no endpoint: the fallback policy (SPEC section 7)
    public async Task Api_paths_and_missing_files_never_fall_back_to_the_page(string path, HttpStatusCode expected)
    {
        await using var factory = await StartAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("fm-test-index", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_build_the_api_serves_no_pages()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["Spa:RootPath"] = Path.Combine(_root, "does-not-exist"),
        });
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/products/x", Ct)).StatusCode);
    }

    private async Task<ApiFactory> StartAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "index.html"), "<!doctype html><title>fm-test-index</title>", Ct);
        await File.WriteAllTextAsync(Path.Combine(_root, "main-I64NK2YK.js"), "console.log('main');", Ct);
        await File.WriteAllTextAsync(Path.Combine(_root, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>", Ct);
        return new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?> { ["Spa:RootPath"] = _root });
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
