using System.ComponentModel.DataAnnotations;

namespace FitnessMerchant.Api.Features.Auth;

public sealed record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(254)] string Email,
    [property: Required, StringLength(50, MinimumLength = 2)] string DisplayName,
    [property: Required, MaxLength(128)] string Password);

public sealed record SignInRequest(
    [property: Required, MaxLength(254)] string Email,
    [property: Required, MaxLength(128)] string Password);

public sealed record UserResponse(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

/// <summary><c>User</c> is null for guests; <c>/me</c> answers 200 either way, so guests see no console errors.</summary>
public sealed record SessionResponse(UserResponse? User);
