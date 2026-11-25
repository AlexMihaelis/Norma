using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Api.Features.Auth.Me;

namespace Norma.Api.Features.Account.UpdateNickname;

/// <summary>
/// Срез «сменить ник»: 2-30 символов, пустое значение стирает ник и возвращает
/// отображение по email.
/// </summary>
public static class UpdateNicknameEndpoint
{
    private const int MinLength = 2;
    private const int MaxLength = 30;

    public static IEndpointRouteBuilder MapUpdateNickname(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut("/me/nickname", Handle)
            .WithName("UpdateNickname")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<MeResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        UpdateNicknameRequest? request,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var nickname = request?.Nickname?.Trim();

        if (!string.IsNullOrEmpty(nickname) && (nickname.Length < MinLength || nickname.Length > MaxLength))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["nickname"] = [$"Ник должен быть от {MinLength} до {MaxLength} символов."],
            });
        }

        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        user.Nickname = string.IsNullOrEmpty(nickname) ? null : nickname;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MeResponseFactory.From(user));
    }
}