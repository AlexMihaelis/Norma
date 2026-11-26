using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Recipes.ListRecipes;

/// <summary>
/// Срез «список рецептов»: свои рецепты, сначала новые. Необязательный
/// <c>q</c> фильтрует по названию - этим же пользуется выбор рецепта в дневнике.
/// </summary>
public static class ListRecipesEndpoint
{
    /// <summary>Потолок списка: рецептов у человека десятки, а не тысячи.</summary>
    private const int MaxItems = 200;

    public static IEndpointRouteBuilder MapListRecipes(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/recipes", Handle)
            .WithName("ListRecipes")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<RecipeListResponse>, UnauthorizedHttpResult>> Handle(
        string? q,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var query = db.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.UserId == userId);

        var search = q?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ILIKE, а не LIKE: «борщ» должен находить «Борщ».
            query = query.Where(recipe => EF.Functions.ILike(recipe.Name, $"%{search}%"));
        }

        var recipes = await query
            .WithComposition()
            .OrderByDescending(recipe => recipe.CreatedAt)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new RecipeListResponse(
            recipes.Select(RecipeMapping.ToSummary).ToList()));
    }
}
