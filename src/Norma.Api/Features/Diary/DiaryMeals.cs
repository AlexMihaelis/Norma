using Microsoft.EntityFrameworkCore;
using Npgsql;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Diary;

/// <summary>
/// Прием пищи на дату: существующий либо только что созданный. Общая операция
/// для записи позиции и повтора дня.
/// </summary>
/// <remarks>
/// Уникальный индекс (UserId, Date, Kind) ловит одновременную вставку: на
/// конфликте свои строки снимаются с отслеживания, а из базы забираются чужие.
/// </remarks>
internal static class DiaryMeals
{
    /// <summary>Приемы нужных типов на дату: найденные и созданные.</summary>
    public static async Task<IReadOnlyDictionary<MealKind, Meal>> GetOrCreateAsync(
        NormaDbContext db,
        Guid userId,
        DateOnly date,
        IReadOnlyCollection<MealKind> kinds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var byKind = (await db.Meals
                .Where(meal => meal.UserId == userId && meal.Date == date)
                .ToListAsync(cancellationToken))
            .ToDictionary(meal => meal.Kind);

        var missing = kinds.Distinct().Where(kind => !byKind.ContainsKey(kind)).ToList();

        if (missing.Count == 0)
        {
            return byKind;
        }

        foreach (var kind in missing)
        {
            var meal = new Meal
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = date,
                Kind = kind,
                CreatedAt = now,
            };

            db.Meals.Add(meal);
            byKind[kind] = meal;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return byKind;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            foreach (var entry in db.ChangeTracker.Entries<Meal>()
                         .Where(entry => entry.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return (await db.Meals
                    .Where(meal => meal.UserId == userId && meal.Date == date)
                    .ToListAsync(cancellationToken))
                .ToDictionary(meal => meal.Kind);
        }
    }

    /// <summary>Прием одного типа на дату.</summary>
    public static async Task<Meal> GetOrCreateAsync(
        NormaDbContext db,
        Guid userId,
        DateOnly date,
        MealKind kind,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var meals = await GetOrCreateAsync(db, userId, date, [kind], now, cancellationToken);

        return meals[kind];
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
