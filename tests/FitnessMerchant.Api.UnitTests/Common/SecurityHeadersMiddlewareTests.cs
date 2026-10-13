using FitnessMerchant.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FitnessMerchant.Api.UnitTests.Common;

public class SecurityHeadersMiddlewareTests
{
    [Theory]
    [InlineData("Development", "/scalar/v1", false)]
    [InlineData("Development", "/openapi/v1.json", false)]
    [InlineData("Development", "/health", true)]
    [InlineData("Production", "/scalar/v1", true)]
    public async Task Csp_is_skipped_only_for_the_api_explorer_in_development(
        string environmentName, string path, bool expectCsp)
    {
        var headers = await RunAsync(environmentName, path);

        Assert.Equal(expectCsp, headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task Adds_baseline_security_headers()
    {
        var headers = await RunAsync("Production", "/");

        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
        Assert.Equal(SecurityHeadersMiddleware.PermissionsPolicy, headers["Permissions-Policy"]);
        Assert.Contains("frame-ancestors 'none'", headers["Content-Security-Policy"].ToString());
    }

    private static async Task<IHeaderDictionary> RunAsync(string environmentName, string path)
    {
        var responseFeature = new StartableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Request.Path = path;

        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, new TestEnvironment(environmentName));
        await middleware.InvokeAsync(context);
        await responseFeature.StartAsync();

        return context.Response.Headers;
    }

    /// <summary>DefaultHttpContext never "starts" the response, so OnStarting callbacks are run by hand.</summary>
    private sealed class StartableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _onStarting)
            {
                await callback(state);
            }
        }
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "FitnessMerchant.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
