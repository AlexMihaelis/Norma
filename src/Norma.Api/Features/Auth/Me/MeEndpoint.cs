using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Norma.Api.Features.Auth.Me;

/// <summary>
/// Защищённый срез «кто я» и образец того, как срезы получают <c>userId</c> из токена.
/// </summary>
public static class MeEndpoint
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/me", Handle)
            .WithName("Me")
            .RequireAuthorization();

        return endpoints;
    }

    private static Results<Ok<MeResponse>, UnauthorizedHttpResult> Handle(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub");

        if (!Guid.TryParse(subject, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var email = principal.FindFirstValue("email") ?? string.Empty;

        return TypedResults.Ok(new MeResponse(userId, email));
    }
}
