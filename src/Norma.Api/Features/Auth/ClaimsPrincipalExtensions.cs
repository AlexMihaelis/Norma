using System.Security.Claims;

namespace Norma.Api.Features.Auth;

/// <summary>Достаёт идентификатор пользователя из проверенного токена.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// <c>userId</c> из claim <c>sub</c>. <c>null</c> - токен без него (например,
    /// выпущен старым форматом), тогда срез обязан вернуть 401.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var userId) ? userId : null;
}
