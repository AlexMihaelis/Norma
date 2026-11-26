using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;
using Norma.Core.Recipes;

namespace Norma.Api.Features.Recipes.ListRecipes;

/// <summary>
/// Срез «список рецептов»: свои рецепты, сначала новые. Фильтры - поиск по
/// названию, метка, автоматическая метка и «только избранное»: этим же будет
/// пользоваться выбор рецепта в дневнике.
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

    private static async Task<Results<Ok<RecipeListResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        string? q,
        string? tag,
        string? autoTag,
        bool? favorite,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var manualTag = default(RecipeTag?);
        var automaticTag = default(RecipeAutoTag?);

        if (!string.IsNullOrWhiteSpace(tag))
        {
            manualTag = RecipeTagCatalog.ParseManual(tag);

            if (manualTag is null)
            {
                errors["tag"] = [$"Неизвестная метка: {tag}."];
            }
        }

        if (!string.IsNullOrWhiteSpace(autoTag))
        {
            automaticTag = RecipeTagCatalog.ParseAuto(autoTag);

            if (automaticTag is null)
            {
                errors["autoTag"] = [$"Неизвестная автоматическая метка: {autoTag}."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
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

        if (manualTag is not null)
        {
            var code = manualTag.Value.ToString();

            query = query.Where(recipe => recipe.Tags.Any(link => link.Tag == code));
        }

        if (favorite is true)
        {
            query = query.Where(recipe => recipe.IsFavorite);
        }

        var recipes = await query
            .WithComposition()
            .OrderByDescending(recipe => recipe.CreatedAt)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        var summaries = recipes.Select(RecipeMapping.ToSummary);

        // Автоматическая метка считается по составу, поэтому фильтр идет после
        // загрузки: в SQL ее не выразить без дублирования правил.
        if (automaticTag is not null)
        {
            var code = automaticTag.Value.ToString();

            summaries = summaries.Where(summary => summary.AutoTags.Contains(code));
        }

        return TypedResults.Ok(new RecipeListResponse(summaries.ToList()));
    }
}
