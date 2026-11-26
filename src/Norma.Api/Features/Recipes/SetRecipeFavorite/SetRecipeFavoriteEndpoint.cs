using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Recipes.SetRecipeFavorite;

/// <summary>
/// Срез «звездочка»: включить или снять отметку избранного. Отдельный маршрут,
/// чтобы форма рецепта не пересохраняла весь состав ради одного флага.
/// </summary>
public static class SetRecipeFavoriteEndpoint
{
    public static IEndpointRouteBuilder MapSetRecipeFavorite(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut("/recipes/{id:guid}/favorite", Handle)
            .WithName("SetRecipeFavorite")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem, UnauthorizedHttpResult>> Handle(
        Guid id,
        FavoriteRecipeRequest? request,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (request?.IsFavorite is not bool isFavorite)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["isFavorite"] = ["Укажите, добавить рецепт в избранное или убрать."],
            });
        }

        var recipe = await db.Recipes.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.UserId == userId,
            cancellationToken);

        if (recipe is null)
        {
            return TypedResults.NotFound();
        }

        if (recipe.IsFavorite != isFavorite)
        {
            recipe.IsFavorite = isFavorite;
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }
}
