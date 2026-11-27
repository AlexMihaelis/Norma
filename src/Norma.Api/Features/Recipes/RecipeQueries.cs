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
    /// Коллекции (строки состава, названия продуктов, метки) грузятся отдельными
    /// запросами: иначе JOIN перемножает их и получаются лишние строки.
    /// </remarks>
    public static IQueryable<Recipe> WithComposition(this IQueryable<Recipe> recipes) =>
        recipes
            .AsSplitQuery()
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(ingredient => ingredient.Food)
            .ThenInclude(food => food!.Names)
            .Include(recipe => recipe.Tags);
}
