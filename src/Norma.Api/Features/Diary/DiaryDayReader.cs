using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Diary;

/// <summary>
/// Чтение дня дневника: позиции с продуктом, шесть блоков приема и итоги.
/// Общая операция для дня и для повтора, чтобы ответ копирования был той же
/// формы, что и обычный день.
/// </summary>
internal static class DiaryDayReader
{
    public static async Task<DiaryDayResponse> ReadAsync(
        NormaDbContext db,
        Guid userId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        // Один запрос на день: позиции вместе с продуктом и названием. КБЖУ
        // считается в памяти: округление позиции совпадает с тем, что видит
        // пользователь, а не с суммой до округления.
        var items = await db.MealItems
            .Where(item => item.Meal!.UserId == userId && item.Meal.Date == day)
            .Include(item => item.Meal)
            .Include(item => item.Food)
            .ThenInclude(food => food!.Names)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        // Все шесть приемов отдаются всегда, даже пустые: UI рисует день целиком
        // и не должен угадывать, какие блоки существуют.
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
