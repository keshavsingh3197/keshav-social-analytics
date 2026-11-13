using System.Security.Claims;

namespace SocialAnalytics.Api.Services;

internal static class CallerIdentityExtensions
{
    public static string? GetCallerUserId(this ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.Identity?.Name;
    }

    public static bool IsCallerAdmin(this ClaimsPrincipal user) =>
        user?.Identity?.IsAuthenticated == true && user.IsInRole("Admin");
}
