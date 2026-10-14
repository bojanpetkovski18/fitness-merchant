using System.ComponentModel.DataAnnotations;

namespace FitnessMerchant.Api.Features.Account;

/// <summary>The length rule is checked again after trimming in the handler, so "  a  " is still too short.</summary>
public sealed record UpdateProfileRequest(
    [property: Required, StringLength(50, MinimumLength = 2)] string DisplayName);

public sealed record ChangePasswordRequest(
    [property: Required, MaxLength(128)] string CurrentPassword,
    [property: Required, MaxLength(128)] string NewPassword);
