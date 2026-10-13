using System.Security.Claims;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace FitnessMerchant.Api.Features.Auth;

public sealed class GetSessionQuery(UserManager<AppUser> users)
{
    public async Task<SessionResponse> ExecuteAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || await users.GetUserAsync(principal) is not { } user)
        {
            return new SessionResponse(null);
        }

        var roles = await users.GetRolesAsync(user);
        return new SessionResponse(new UserResponse(user.Id, user.Email!, user.DisplayName, [.. roles.Order()]));
    }
}

public sealed class RegisterHandler(
    AppDbContext db,
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    GetSessionQuery session,
    XsrfCookie xsrf,
    TimeProvider time)
{
    public async Task<Results<Created<SessionResponse>, ValidationProblem>> ExecuteAsync(
        RegisterRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = time.GetUtcNow(),
        };

        // The role is always Customer (never taken from the request) and is saved with the user atomically.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return TypedResults.ValidationProblem(ToFieldErrors(created.Errors));
        }

        await users.AddToRoleAsync(user, Roles.Customer);
        await transaction.CommitAsync(cancellationToken);

        await signIn.SignInAsync(user, isPersistent: true);
        context.User = await signIn.CreateUserPrincipalAsync(user);
        xsrf.Issue(context);

        return TypedResults.Created("/api/auth/me", await session.ExecuteAsync(context.User));
    }

    private static Dictionary<string, string[]> ToFieldErrors(IEnumerable<IdentityError> errors) =>
        errors
            .Select(e => e.Code switch
            {
                nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)
                    => (Field: "email", Message: "An account with this email already exists."),
                nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName)
                    => (Field: "email", Message: "Enter a valid email address."),
                _ when e.Code.StartsWith("Password", StringComparison.Ordinal) => (Field: "password", Message: e.Description),
                _ => (Field: "", Message: e.Description),
            })
            .GroupBy(e => e.Field)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).Distinct().ToArray());
}

public sealed class SignInHandler(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    GetSessionQuery session,
    XsrfCookie xsrf)
{
    private static readonly AppUser TimingDummy = new() { DisplayName = "-" };

    public async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> ExecuteAsync(SignInRequest request, HttpContext context)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Spend the same hashing time as a real check, so response times do not reveal which emails exist.
            users.PasswordHasher.HashPassword(TimingDummy, request.Password);
            return InvalidCredentials();
        }

        // Locked-out accounts get the same answer as wrong passwords: no account enumeration.
        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        context.User = await signIn.CreateUserPrincipalAsync(user);
        xsrf.Issue(context);
        return TypedResults.Ok(await session.ExecuteAsync(context.User));
    }

    private static ProblemHttpResult InvalidCredentials() =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
}
