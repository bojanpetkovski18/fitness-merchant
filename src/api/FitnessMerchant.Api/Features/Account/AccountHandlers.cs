using System.Security.Claims;
using FitnessMerchant.Api.Features.Auth;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace FitnessMerchant.Api.Features.Account;

public sealed class UpdateProfileHandler(UserManager<AppUser> users, GetSessionQuery session)
{
    public async Task<Results<Ok<SessionResponse>, ValidationProblem, UnauthorizedHttpResult>> ExecuteAsync(
        UpdateProfileRequest request, ClaimsPrincipal principal)
    {
        var displayName = request.DisplayName.Trim();
        if (displayName.Length < 2)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["displayName"] = ["Enter a name of 2 to 50 characters."],
            });
        }

        if (await users.GetUserAsync(principal) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        user.DisplayName = displayName;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new InvalidOperationException($"Profile update failed: {string.Join(", ", updated.Errors.Select(e => e.Code))}");
        }

        return TypedResults.Ok(await session.ExecuteAsync(principal));
    }
}

/// <summary>
/// Changing the password rotates the security stamp (Identity does this), which ends every other session on the
/// next request (D025). This session is re-issued with the new stamp so the user stays signed in here.
/// </summary>
public sealed class ChangePasswordHandler(UserManager<AppUser> users, SignInManager<AppUser> signIn, XsrfCookie xsrf)
{
    public async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> ExecuteAsync(
        ChangePasswordRequest request, HttpContext context)
    {
        if (await users.GetUserAsync(context.User) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
        {
            return TypedResults.ValidationProblem(ToFieldErrors(changed.Errors));
        }

        await signIn.RefreshSignInAsync(user);
        context.User = await signIn.CreateUserPrincipalAsync(user);
        xsrf.Issue(context);
        return TypedResults.NoContent();
    }

    private static Dictionary<string, string[]> ToFieldErrors(IEnumerable<IdentityError> errors) =>
        errors
            .Select(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                ? (Field: "currentPassword", Message: "Your current password is not correct.")
                : (Field: "newPassword", Message: e.Description))
            .GroupBy(e => e.Field)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).Distinct().ToArray());
}
