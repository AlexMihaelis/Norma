using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Api.Features.Auth.Me;

namespace Norma.Api.Features.Account.DeleteAvatar;

/// <summary>Срез «убрать аватар»: возвращает отображение по нику или email.</summary>
public static class DeleteAvatarEndpoint
{
    public static IEndpointRouteBuilder MapDeleteAvatar(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/me/avatar", Handle)
            .WithName("DeleteAvatar")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        user.AvatarContentType = null;
        user.AvatarBytes = null;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MeResponseFactory.From(user));
    }
}