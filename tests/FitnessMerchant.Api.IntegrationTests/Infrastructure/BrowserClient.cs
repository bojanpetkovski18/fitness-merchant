using System.Net;
using System.Net.Http.Json;
using FitnessMerchant.Api.Features.Auth;

namespace FitnessMerchant.Api.IntegrationTests.Infrastructure;

/// <summary>Wraps the API like the Angular app uses it: load the session first (gets the XSRF cookie), then act.</summary>
public sealed class BrowserClient(HttpClient http, CookieContainer cookies, XsrfHeaderHandler xsrf) : IDisposable
{
    public static readonly Uri BaseAddress = new("https://localhost");
    public const string Password = "Correct-Horse-9";

    public HttpClient Http { get; } = http;
    public CookieContainer Cookies { get; } = cookies;

    /// <summary>Turn off to simulate a cross-site request that cannot read the XSRF cookie.</summary>
    public bool SendXsrfHeader { get => xsrf.Enabled; set => xsrf.Enabled = value; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task<SessionResponse> MeAsync()
    {
        var response = await Http.GetAsync("/api/auth/me", Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(Ct))!;
    }

    public async Task<HttpResponseMessage> RegisterAsync(string email, string password = Password, string displayName = "Test Shopper", object? extra = null)
    {
        await EnsureXsrfAsync();
        object body = extra ?? new { email, displayName, password };
        return await Http.PostAsJsonAsync("/api/auth/register", body, Ct);
    }

    public async Task<HttpResponseMessage> SignInAsync(string email, string password = Password)
    {
        await EnsureXsrfAsync();
        return await Http.PostAsJsonAsync("/api/auth/sign-in", new { email, password }, Ct);
    }

    public async Task<HttpResponseMessage> SignOutAsync()
    {
        await EnsureXsrfAsync();
        return await Http.PostAsync("/api/auth/sign-out", content: null, Ct);
    }

    public static string NewEmail() => $"shopper-{Guid.NewGuid():N}@test.example";

    private async Task EnsureXsrfAsync()
    {
        if (Cookies.GetCookies(BaseAddress)["XSRF-TOKEN"] is null)
        {
            await MeAsync();
        }
    }

    public void Dispose() => Http.Dispose();
}
