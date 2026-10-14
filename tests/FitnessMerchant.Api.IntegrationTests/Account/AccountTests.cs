using System.Net;
using System.Net.Http.Json;
using FitnessMerchant.Api.Features.Auth;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace FitnessMerchant.Api.IntegrationTests.Account;

public class AccountTests(PostgresFixture postgres)
{
    private const string NewPassword = "Brand-New-Pass-7";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Profile_update_trims_and_saves_the_display_name()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = await SignedInAsync(factory);

        var response = await browser.Http.PutAsJsonAsync("/api/account/profile", new { displayName = "  Mila Novak  " }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mila Novak", (await response.Content.ReadFromJsonAsync<SessionResponse>(Ct))!.User!.DisplayName);
        Assert.Equal("Mila Novak", (await browser.MeAsync()).User!.DisplayName);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("   a   ")] // long enough before trimming, too short after
    [InlineData("")]
    [InlineData("This display name is far too long to fit in fifty chars")]
    public async Task Profile_update_rejects_invalid_names(string displayName)
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = await SignedInAsync(factory);

        var response = await browser.Http.PutAsJsonAsync("/api/account/profile", new { displayName }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!;
        Assert.Contains(problem.Errors.Keys, key => key.Equals("displayName", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Test Shopper", (await browser.MeAsync()).User!.DisplayName);
    }

    [Fact]
    public async Task Account_endpoints_require_a_signed_in_user()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        await browser.MeAsync();

        var profile = await browser.Http.PutAsJsonAsync("/api/account/profile", new { displayName = "Anon" }, Ct);
        var password = await ChangePasswordAsync(browser, BrowserClient.Password, NewPassword);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (profile.StatusCode, password.StatusCode));
    }

    [Fact]
    public async Task Wrong_current_password_is_rejected_and_nothing_changes()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        var email = BrowserClient.NewEmail();
        using var browser = await SignedInAsync(factory, email);

        var response = await ChangePasswordAsync(browser, "Not-My-Password-1", NewPassword);

        var problem = await ValidationProblemAsync(response);
        Assert.Equal(["Your current password is not correct."], problem.Errors["currentPassword"]);
        Assert.NotNull((await browser.MeAsync()).User); // still signed in
        using var other = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await other.SignInAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Weak_new_password_is_reported_on_newPassword()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = await SignedInAsync(factory);

        var response = await ChangePasswordAsync(browser, BrowserClient.Password, "short");

        Assert.NotEmpty((await ValidationProblemAsync(response)).Errors["newPassword"]);
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_every_other_one()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        var email = BrowserClient.NewEmail();
        using var laptop = await SignedInAsync(factory, email);
        using var phone = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await phone.SignInAsync(email)).StatusCode);
        var phoneCookieBefore = phone.Cookies.GetCookies(BrowserClient.BaseAddress)["fm_session"]!.Value;

        var response = await ChangePasswordAsync(laptop, BrowserClient.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(email, (await laptop.MeAsync()).User?.Email);
        Assert.Equal(HttpStatusCode.OK, (await laptop.Http.GetAsync("/api/orders", Ct)).StatusCode);

        // The other device still holds its old cookie, and it no longer works.
        Assert.Equal(phoneCookieBefore, phone.Cookies.GetCookies(BrowserClient.BaseAddress)["fm_session"]!.Value);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.Http.GetAsync("/api/orders", Ct)).StatusCode);
        Assert.Null((await phone.MeAsync()).User);

        using var fresh = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.SignInAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fresh.SignInAsync(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Password_changes_are_rate_limited()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = "3",
        });
        using var browser = await SignedInAsync(factory); // register uses 1 of the 3

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangePasswordAsync(browser, "Guess-Number-1", NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangePasswordAsync(browser, "Guess-Number-2", NewPassword)).StatusCode);
        var limited = await ChangePasswordAsync(browser, BrowserClient.Password, NewPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task Passwords_never_appear_in_the_logs()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = await SignedInAsync(factory);

        await ChangePasswordAsync(browser, "Wrong-Current-1", NewPassword);
        await ChangePasswordAsync(browser, BrowserClient.Password, NewPassword);

        Assert.NotEmpty(factory.Logs.Lines);
        Assert.DoesNotContain(factory.Logs.Lines, l =>
            l.Contains(BrowserClient.Password, StringComparison.Ordinal) ||
            l.Contains(NewPassword, StringComparison.Ordinal) ||
            l.Contains("Wrong-Current-1", StringComparison.Ordinal));
    }

    private static async Task<BrowserClient> SignedInAsync(ApiFactory factory, string? email = null)
    {
        var browser = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.Created, (await browser.RegisterAsync(email ?? BrowserClient.NewEmail())).StatusCode);
        return browser;
    }

    private static Task<HttpResponseMessage> ChangePasswordAsync(BrowserClient browser, string currentPassword, string newPassword) =>
        browser.Http.PostAsJsonAsync("/api/account/password", new { currentPassword, newPassword }, Ct);

    private static async Task<HttpValidationProblemDetails> ValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!;
        return new HttpValidationProblemDetails(problem.Errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase));
    }
}
