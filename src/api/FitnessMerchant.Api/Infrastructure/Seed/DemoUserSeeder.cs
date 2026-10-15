using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace FitnessMerchant.Api.Infrastructure.Seed;

/// <summary>
/// Creates the demo admin and customer from environment settings (never from the repository). An account that
/// already exists is left unchanged, including its password. Missing settings skip that user with a warning.
/// </summary>
public sealed partial class DemoUserSeeder(
    UserManager<AppUser> users,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<DemoUserSeeder> logger)
{
    public async Task SeedAsync() => await SeedAsync(restore: false);

    /// <param name="restore">
    /// The demo reset: an existing demo account gets its name and role back and any lockout cleared. The password is
    /// not touched; demo mode does not let anyone change it.
    /// </param>
    public async Task SeedAsync(bool restore)
    {
        await EnsureUserAsync("DEMO_ADMIN", "Demo Admin", Roles.Admin, restore);
        await EnsureUserAsync("DEMO_CUSTOMER", "Demo Customer", Roles.Customer, restore);
    }

    /// <summary>The configured demo emails (normalized), for the reset and for demo-mode guards.</summary>
    public static IReadOnlyList<string> DemoEmails(IConfiguration configuration) =>
        [.. new[] { configuration["DEMO_ADMIN_EMAIL"], configuration["DEMO_CUSTOMER_EMAIL"] }
            .Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!.Trim().ToUpperInvariant())];

    private async Task EnsureUserAsync(string prefix, string displayName, string role, bool restore)
    {
        var email = configuration[$"{prefix}_EMAIL"];
        var password = configuration[$"{prefix}_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            LogMissing(logger, prefix);
            return;
        }

        if (await users.FindByEmailAsync(email) is { } existing)
        {
            if (restore)
            {
                existing.DisplayName = displayName;
                await users.UpdateAsync(existing);
                await users.SetLockoutEndDateAsync(existing, null);
                await users.ResetAccessFailedCountAsync(existing);
                if (!await users.IsInRoleAsync(existing, role))
                {
                    await users.AddToRoleAsync(existing, role);
                }
            }
            return;
        }

        var user = new AppUser { UserName = email, Email = email, DisplayName = displayName, CreatedAt = time.GetUtcNow() };
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
        {
            result = await users.AddToRoleAsync(user, role);
        }

        if (result.Succeeded)
        {
            LogCreated(logger, role);
        }
        else
        {
            // Never log the password; the error codes say what to fix (e.g. PasswordRequiresDigit).
            LogFailed(logger, prefix, string.Join(", ", result.Errors.Select(e => e.Code)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Prefix}_EMAIL or {Prefix}_PASSWORD is not set; that demo user was not created.")]
    private static partial void LogMissing(ILogger logger, string prefix);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the demo {Role} account.")]
    private static partial void LogCreated(ILogger logger, string role);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create the {Prefix} demo user: {Errors}.")]
    private static partial void LogFailed(ILogger logger, string prefix, string errors);
}
