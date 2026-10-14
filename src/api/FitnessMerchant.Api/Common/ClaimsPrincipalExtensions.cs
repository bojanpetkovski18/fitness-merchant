using System.Security.Claims;

namespace FitnessMerchant.Api.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in user's ID from the session cookie. "My" resources (cart, orders) are always looked up by
    /// this, never by an ID sent in the request (CLAUDE.md security rules; v1's IDOR).
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("No signed-in user; the endpoint must require authorization.");
}
