using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Recipes.DeleteRecipe;

/// <summary>
/// Срез «удалить рецепт». Рецепт, который уже попал в дневник, не удаляется:
/// иначе история питания потеряет строку. Ответ - 409, как у конфликта.
/// </summary>
public static class DeleteRecipeEndpoint
{
    public static IEndpointRouteBuilder MapDeleteRecipe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/recipes/{id:guid}", Handle)
            .WithName("DeleteRecipe")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<NoContent, NotFound, Conflict<string>, UnauthorizedHttpResult>> Handle(
        Guid id,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var recipe = await db.Recipes
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id && candidate.UserId == userId,
                cancellationToken);

        if (recipe is null)
        {
            return TypedResults.NotFound();
        }

        db.Recipes.Remove(recipe);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Состав рецепта уходит каскадом, а вот позиции дневника держат
            // рецепт через Restrict - это осознанный конфликт, а не сбой.
            return TypedResults.Conflict("Рецепт уже есть в дневнике: удалять его нельзя.");
        }

        return TypedResults.NoContent();
    }
}
