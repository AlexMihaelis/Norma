using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Recipes.GetRecipe;

/// <summary>Срез «рецепт целиком»: состав и посчитанные из него цифры.</summary>
public static class GetRecipeEndpoint
{
    public static IEndpointRouteBuilder MapGetRecipe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/recipes/{id:guid}", Handle)
            .WithName("GetRecipe")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<RecipeResponse>, NotFound, UnauthorizedHttpResult>> Handle(
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

        // Фильтр по владельцу в самом запросе: чужой рецепт неотличим от несуществующего.
        var recipe = await db.Recipes
            .AsNoTracking()
            .Where(candidate => candidate.Id == id && candidate.UserId == userId)
            .WithComposition()
            .FirstOrDefaultAsync(cancellationToken);

        return recipe is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(RecipeMapping.ToResponse(recipe));
    }
}
