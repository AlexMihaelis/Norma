using Microsoft.EntityFrameworkCore;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Recipes;

/// <summary>Загрузка рецепта вместе с составом: без продуктов цифры не посчитать.</summary>
internal static class RecipeQueries
{
    /// <summary>
    /// Состав рецепта с продуктами и их названиями: одна форма загрузки для
    /// списка, карточки и замены, чтобы нигде не забыть Include.
    /// </summary>
    /// <remarks>
    /// Две коллекции в одном запросе (строки состава и названия продуктов) дают
    /// декартово произведение, поэтому грузим их отдельными запросами.
    /// </remarks>
    public static IQueryable<Recipe> WithComposition(this IQueryable<Recipe> recipes) =>
        recipes
            .AsSplitQuery()
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(ingredient => ingredient.Food)
            .ThenInclude(food => food!.Names);
}
