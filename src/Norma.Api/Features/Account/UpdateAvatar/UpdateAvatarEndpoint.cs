using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Api.Features.Auth.Me;

namespace Norma.Api.Features.Account.UpdateAvatar;

/// <summary>
/// Срез «загрузить аватар»: принимает data URL картинки, проверяет формат и размер
/// и хранит байты в строке пользователя.
/// </summary>
public static class UpdateAvatarEndpoint
{
    public static IEndpointRouteBuilder MapUpdateAvatar(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut("/me/avatar", Handle)
            .WithName("UpdateAvatar")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<MeResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        UpdateAvatarRequest? request,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!AvatarData.TryParse(request?.DataUrl, out var mime, out var bytes, out var error))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["dataUrl"] = [error!],
            });
        }

        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        user.AvatarContentType = mime;
        user.AvatarBytes = bytes;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MeResponseFactory.From(user));
    }
}