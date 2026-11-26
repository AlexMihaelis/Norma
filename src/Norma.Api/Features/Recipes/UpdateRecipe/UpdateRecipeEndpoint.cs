using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Recipes.UpdateRecipe;

/// <summary>
/// Срез «замена рецепта»: состав заменяется целиком, КБЖУ пересчитывается из
/// нового состава при следующем чтении.
/// </summary>
public static class UpdateRecipeEndpoint
{
    public static IEndpointRouteBuilder MapUpdateRecipe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut("/recipes/{id:guid}", Handle)
            .WithName("UpdateRecipe")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<RecipeResponse>, NotFound, ValidationProblem, UnauthorizedHttpResult>> Handle(
        Guid id,
        RecipeRequest? request,
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

        var draft = await RecipeDraft.BuildAsync(request, db, cancellationToken);

        if (!draft.IsValid)
        {
            return TypedResults.ValidationProblem(draft.Errors);
        }

        recipe.Name = draft.Name;
        recipe.Instructions = draft.Instructions;

        // Фото в теле нет - значит его снимают: замена полная.
        recipe.PhotoContentType = draft.PhotoContentType;
        recipe.PhotoBytes = draft.PhotoBytes;

        // Старый состав и метки удаляем запросами, не поднимая строки в контекст:
        // тогда уникальные индексы не спорят между удалением и вставкой.
        await db.RecipeIngredients
            .Where(ingredient => ingredient.RecipeId == recipe.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await db.RecipeTags
            .Where(tag => tag.RecipeId == recipe.Id)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var tag in draft.Tags)
        {
            db.RecipeTags.Add(new RecipeTagLink
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                Tag = tag.ToString(),
            });
        }

        foreach (var (food, grams) in draft.Lines)
        {
            db.RecipeIngredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                FoodId = food.Id,
                Food = food,
                Grams = grams,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.Recipes
            .Where(candidate => candidate.Id == recipe.Id)
            .WithComposition()
            .FirstAsync(cancellationToken);

        return TypedResults.Ok(RecipeMapping.ToResponse(updated));
    }
}
