using System.Security.Claims;
using Opdeweg.Application.Common;

namespace Opdeweg.Api.Extensions;

internal static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var id) ? id : null;

    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        principal.GetUserIdOrNull() ?? throw AppException.Unauthorized("unauthenticated", "Log in om verder te gaan.");
}
