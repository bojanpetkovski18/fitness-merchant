namespace FitnessMerchant.Api.Features.Health;

/// <summary>
/// <c>dotnet FitnessMerchant.Api.dll --health-check</c>: asks the running app on this machine for <c>/health/live</c>
/// and exits 0 (healthy) or 1. The container image has no shell or curl (chiseled), so its HEALTHCHECK uses this.
/// </summary>
public static class HealthProbe
{
    public const string Argument = "--health-check";

    public static async Task<int> RunAsync()
    {
        // ASPNETCORE_HTTP_PORTS is what the image listens on (8080 by default in the .NET images).
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/health/live"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
