using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Diary;

/// <summary>
/// Чтение дня дневника: позиции с продуктом или составом рецепта, шесть блоков
/// приема и итоги. Общая операция для дня и для повтора.
/// </summary>
internal static class DiaryDayReader
{
    public static async Task<DiaryDayResponse> ReadAsync(
        NormaDbContext db,
        Guid userId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        // КБЖУ считается в памяти: округление позиции должно совпадать с тем, что
        // увидит пользователь.
        var items = await db.MealItems
            .Where(item => item.Meal!.UserId == userId && item.Meal.Date == day)
            .Include(item => item.Meal)
            .Include(item => item.Food)
            .ThenInclude(food => food!.Names)
            .Include(item => item.Recipe)
            .ThenInclude(recipe => recipe!.Ingredients)
            .ThenInclude(ingredient => ingredient.Food)
            .ThenInclude(food => food!.Names)
            .AsSplitQuery()
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        // Все шесть приемов отдаются всегда: UI рисует день целиком.
        var meals = Enum.GetValues<MealKind>()
            .Select(kind =>
            {
                var mealItems = items
                    .Where(item => item.Meal!.Kind == kind)
                    .Select(DiaryMapping.ToEntry)
                    .ToList();

                return new DiaryMealResponse(kind, mealItems, DiaryMapping.Totals(mealItems));
            })
            .ToList();

        return new DiaryDayResponse(
            day,
            meals,
            DiaryMapping.Totals(meals.SelectMany(meal => meal.Items)));
    }
}
