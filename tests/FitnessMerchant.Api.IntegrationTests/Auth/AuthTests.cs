using System.Net;
using System.Net.Http.Json;
using FitnessMerchant.Api.Features.Auth;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Identity;
using FitnessMerchant.Api.Infrastructure.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessMerchant.Api.IntegrationTests.Auth;

public class AuthTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_signs_in_as_customer_then_sign_out_ends_the_session()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        var email = BrowserClient.NewEmail();

        Assert.Null((await browser.MeAsync()).User);

        var registered = await browser.RegisterAsync(email);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var session = await registered.Content.ReadFromJsonAsync<SessionResponse>(Ct);
        Assert.Equal(email, session!.User!.Email);
        Assert.Equal(["Customer"], session.User.Roles);

        Assert.Equal(email, (await browser.MeAsync()).User?.Email);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.SignOutAsync()).StatusCode);
        Assert.Null((await browser.MeAsync()).User);
    }

    [Fact]
    public async Task Role_in_the_register_body_is_ignored()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        var email = BrowserClient.NewEmail();

        var response = await browser.RegisterAsync(email, extra: new
        {
            email,
            displayName = "Sneaky",
            password = BrowserClient.Password,
            role = "Admin",
            roles = new[] { "Admin" },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["Customer"], (await browser.MeAsync()).User!.Roles);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.Http.GetAsync("/api/test/admin-only", Ct)).StatusCode);
    }

    [Fact]
    public async Task Registering_an_existing_email_is_a_validation_error_on_email()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var first = factory.CreateBrowserClient();
        using var second = factory.CreateBrowserClient();
        var email = BrowserClient.NewEmail();
        await first.RegisterAsync(email);

        var response = await second.RegisterAsync(email.ToUpperInvariant());

        var problem = await ValidationProblemAsync(response);
        Assert.Contains("already exists", Assert.Single(problem.Errors["email"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Weak_password_is_rejected_with_password_errors()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();

        var response = await browser.RegisterAsync(BrowserClient.NewEmail(), password: "short");

        var problem = await ValidationProblemAsync(response);
        Assert.NotEmpty(problem.Errors["password"]);
    }

    [Theory]
    [InlineData("not-an-email", "Valid Name", "Email")]
    [InlineData("ok@test.example", "X", "DisplayName")]
    public async Task Invalid_register_input_is_rejected_by_server_validation(string email, string displayName, string field)
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();

        var response = await browser.RegisterAsync(email, displayName: displayName);

        var problem = await ValidationProblemAsync(response);
        Assert.Contains(problem.Errors.Keys, key => key.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_generic_401()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        var email = BrowserClient.NewEmail();
        await browser.RegisterAsync(email);
        await browser.SignOutAsync();

        var wrongPassword = await browser.SignInAsync(email, "Wrong-Password-1");
        var unknownEmail = await browser.SignInAsync(BrowserClient.NewEmail());

        foreach (var response in new[] { wrongPassword, unknownEmail })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
            Assert.Equal("Invalid email or password.", problem!.Title);
        }
    }

    [Fact]
    public async Task Account_locks_after_five_failed_attempts()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        var email = BrowserClient.NewEmail();
        await browser.RegisterAsync(email);
        await browser.SignOutAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SignInAsync(email, "Wrong-Password-1")).StatusCode);
        }

        // Even the correct password is refused while locked out, with the same generic answer.
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SignInAsync(email)).StatusCode);
        Assert.Null((await browser.MeAsync()).User);
    }

    [Fact]
    public async Task Sign_in_is_rate_limited_per_client_with_429_and_retry_after()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = "3",
        });
        using var browser = factory.CreateBrowserClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SignInAsync("nobody@test.example")).StatusCode);
        }
        var limited = await browser.SignInAsync("nobody@test.example");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.True(limited.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task Unsafe_request_without_the_antiforgery_header_is_rejected()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        await browser.MeAsync(); // has the cookies, like a page the attacker cannot read
        browser.SendXsrfHeader = false;

        var response = await browser.RegisterAsync(BrowserClient.NewEmail());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Invalid or missing anti-forgery token.", problem!.Title);
        Assert.Null((await browser.MeAsync()).User);
    }

    [Fact]
    public async Task Antiforgery_token_from_before_sign_in_is_replaced_after_sign_in()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        await browser.MeAsync();
        var anonymousToken = browser.Cookies.GetCookies(BrowserClient.BaseAddress)["XSRF-TOKEN"]!.Value;

        await browser.RegisterAsync(BrowserClient.NewEmail());
        var signedInToken = browser.Cookies.GetCookies(BrowserClient.BaseAddress)["XSRF-TOKEN"]!.Value;

        Assert.NotEqual(anonymousToken, signedInToken);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SignOutAsync()).StatusCode);
    }

    [Fact]
    public async Task Admin_policy_allows_admins_only()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var anonymous = factory.CreateBrowserClient();
        using var customer = factory.CreateBrowserClient();
        using var admin = factory.CreateBrowserClient();
        await customer.RegisterAsync(BrowserClient.NewEmail());
        var adminEmail = BrowserClient.NewEmail();
        await admin.RegisterAsync(adminEmail);
        await PromoteToAdminAsync(factory, adminEmail);
        await admin.SignOutAsync();
        await admin.SignInAsync(adminEmail); // roles are read at sign-in

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.Http.GetAsync("/api/test/admin-only", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Http.GetAsync("/api/test/admin-only", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.Http.GetAsync("/api/test/admin-only", Ct)).StatusCode);
    }

    [Fact]
    public async Task Fallback_policy_protects_endpoints_without_auth_metadata()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();

        var anonymous = await browser.Http.GetAsync("/api/test/unannotated", Ct);
        await browser.RegisterAsync(BrowserClient.NewEmail());
        var signedIn = await browser.Http.GetAsync("/api/test/unannotated", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("application/problem+json", anonymous.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task Session_cookie_is_httponly_secure_and_samesite_strict()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();

        var response = await browser.RegisterAsync(BrowserClient.NewEmail());

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var session = Assert.Single(setCookies, c => c.StartsWith(AuthSetup.SessionCookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", session, StringComparison.OrdinalIgnoreCase);
        var xsrf = Assert.Single(setCookies, c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", xsrf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_session_cookie_stops_working_once_the_security_stamp_changes()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var browser = factory.CreateBrowserClient();
        var email = BrowserClient.NewEmail();
        await browser.RegisterAsync(email);
        Assert.Equal(email, (await browser.MeAsync()).User?.Email);

        using (var scope = factory.Services.CreateScope())
        {
            // What a password change (or a future "sign out everywhere") does to the stored user.
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await users.UpdateSecurityStampAsync((await users.FindByEmailAsync(email))!);
        }

        Assert.Null((await browser.MeAsync()).User);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Http.GetAsync("/api/test/unannotated", Ct)).StatusCode);
    }

    [Fact]
    public async Task Demo_users_are_seeded_from_settings_and_can_sign_in()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["DEMO_ADMIN_EMAIL"] = "admin@fitness-merchant.example",
            ["DEMO_ADMIN_PASSWORD"] = "Demo-Admin-Pass-1",
            // DEMO_CUSTOMER_* deliberately missing: that user is skipped.
        });
        using (var scope = factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoUserSeeder>();
            await seeder.SeedAsync();
            await seeder.SeedAsync(); // idempotent
        }
        using var browser = factory.CreateBrowserClient();

        var response = await browser.SignInAsync("admin@fitness-merchant.example", "Demo-Admin-Pass-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Admin"], (await browser.MeAsync()).User!.Roles);
        using var check = factory.Services.CreateScope();
        Assert.Null(await check.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync("customer@fitness-merchant.example"));
    }

    private static async Task PromoteToAdminAsync(ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email);
        await users.AddToRoleAsync(user!, Roles.Admin);
    }

    private static async Task<HttpValidationProblemDetails> ValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct);
        Assert.NotNull(problem);
        return new HttpValidationProblemDetails(
            problem.Errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase));
    }
}
