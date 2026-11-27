using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Recipes.CreateRecipe;

/// <summary>Срез «новый рецепт»: состав проверяется, КБЖУ не сохраняется.</summary>
public static class CreateRecipeEndpoint
{
    public static IEndpointRouteBuilder MapCreateRecipe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/recipes", Handle)
            .WithName("CreateRecipe")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<RecipeResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
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

        var draft = await RecipeDraft.BuildAsync(request, db, cancellationToken);

        if (!draft.IsValid)
        {
            return TypedResults.ValidationProblem(draft.Errors);
        }

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = draft.Name,
            Instructions = draft.Instructions,
            PhotoContentType = draft.PhotoContentType,
            PhotoBytes = draft.PhotoBytes,
            ManualKcalPer100g = draft.KcalPer100g,
            ManualProteinPer100g = draft.ProteinPer100g,
            ManualFatPer100g = draft.FatPer100g,
            ManualCarbsPer100g = draft.CarbsPer100g,
            CookingMinutes = draft.CookingMinutes,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        foreach (var tag in draft.Tags)
        {
            recipe.Tags.Add(new RecipeTagLink
            {
                Id = Guid.NewGuid(),
                Tag = tag.ToString(),
            });
        }

        foreach (var (food, grams) in draft.Lines)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                FoodId = food.Id,
                Food = food,
                Grams = grams,
            });
        }

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/v1/recipes/{recipe.Id}", RecipeMapping.ToResponse(recipe));
    }
}
