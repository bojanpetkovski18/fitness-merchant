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

    [Fact]
    public async Task Spa_files_are_compressed_but_api_responses_never_are()
    {
        // M12 Lighthouse: the bundles went out uncompressed. API JSON stays uncompressed on purpose (BREACH).
        await using var factory = await StartAsync();
        using var client = factory.CreateClient();

        async Task<string?> EncodingOf(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.AcceptEncoding.ParseAdd("br, gzip");
            var response = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return response.Content.Headers.ContentEncoding.SingleOrDefault();
        }

        Assert.Equal("br", await EncodingOf("/main-I64NK2YK.js"));
        Assert.Equal("br", await EncodingOf("/products/whey-isolate-vanilla-2-kg")); // index.html from the fallback
        Assert.Null(await EncodingOf("/api/catalog/departments"));
        Assert.Null(await EncodingOf("/api/meta"));
    }

    [Fact]
    public async Task Precompressed_copies_from_the_build_are_served_as_is()
    {
        await using var factory = await StartAsync();
        byte[] precompressed = [0x1b, 0x13, 0x00, 0xf8, 0x25, 0x82]; // stands in for "main-I64NK2YK.js.br"
        await File.WriteAllBytesAsync(Path.Combine(_root, "main-I64NK2YK.js.br"), precompressed, Ct);
        using var client = factory.CreateClient();

        using var brotli = new HttpRequestMessage(HttpMethod.Get, "/main-I64NK2YK.js");
        brotli.Headers.AcceptEncoding.ParseAdd("gzip, br");
        var compressed = await client.SendAsync(brotli, Ct);
        var plain = await client.GetAsync("/main-I64NK2YK.js", Ct);

        Assert.Equal(precompressed, await compressed.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal("br", compressed.Content.Headers.ContentEncoding.Single());
        Assert.Equal("text/javascript", compressed.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept-Encoding", compressed.Headers.Vary);
        Assert.Equal("public, max-age=31536000, immutable", compressed.Headers.CacheControl?.ToString());
        Assert.Equal("console.log('main');", await plain.Content.ReadAsStringAsync(Ct));
        Assert.Empty(plain.Content.Headers.ContentEncoding);
    }

    [Theory]
    [InlineData("fitness-merchant.example", true)]
    [InlineData("localhost", false)]
    public async Task Https_responses_carry_hsts_outside_development_except_for_localhost(string host, bool expected)
    {
        await using var factory = await StartAsync();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri($"https://{host}") });

        var response = await client.GetAsync("/", Ct);

        Assert.Equal(expected, response.Headers.TryGetValues("Strict-Transport-Security", out var values));
        if (expected)
        {
            Assert.Equal("max-age=31536000", values!.Single());
        }
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
