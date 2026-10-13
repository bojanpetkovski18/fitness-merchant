using Microsoft.AspNetCore.Identity;

namespace FitnessMerchant.Api.Infrastructure.Identity;

/// <summary>A store account. Sign-in uses the email; <see cref="IdentityUser{TKey}.UserName"/> mirrors it.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    /// <summary>Fixed IDs so the roles can be created by the migration itself (registration needs them).</summary>
    internal static readonly IdentityRole<Guid>[] All =
    [
        new() { Id = new Guid("0199a000-0000-7000-8000-000000000001"), Name = Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "role-admin" },
        new() { Id = new Guid("0199a000-0000-7000-8000-000000000002"), Name = Customer, NormalizedName = "CUSTOMER", ConcurrencyStamp = "role-customer" },
    ];
}
