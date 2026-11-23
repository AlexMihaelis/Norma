using Microsoft.EntityFrameworkCore;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Seeding;

/// <summary>
/// Загружает стартовый набор продуктов, не трогая уже существующие записи: если
/// продукт с таким русским названием есть (в том числе свой у пользователя), он
/// пропускается.
/// </summary>
internal static class FoodSeeder
{
    public static async Task<int> SeedAsync(NormaDbContext db, CancellationToken cancellationToken)
    {
        var existingNames = await db.FoodNames
            .Where(name => name.Locale == "ru")
            .Select(name => name.Name.ToLower())
            .ToListAsync(cancellationToken);

        var known = existingNames.ToHashSet(StringComparer.Ordinal);

        var missing = FoodSeed.Items
            .Where(item => !known.Contains(item.Name.ToLower()))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        foreach (var item in missing)
        {
            db.Foods.Add(Create(item));
        }

        await db.SaveChangesAsync(cancellationToken);

        return missing.Count;
    }

    private static Food Create(FoodSeedItem item)
    {
        var food = new Food
        {
            Id = Guid.NewGuid(),
            Source = FoodSource.Seed,
            SourceId = null,
            State = item.State,
            BaseUnit = item.Unit,
            KcalPer100g = item.Kcal,
            ProteinPer100g = item.Protein,
            FatPer100g = item.Fat,
            CarbsPer100g = item.Carbs,
        };

        food.Names.Add(new FoodName
        {
            Id = Guid.NewGuid(),
            Locale = "ru",
            Name = item.Name,
            IsPrimary = true,
        });

        foreach (var alias in item.Aliases ?? [])
        {
            food.Aliases.Add(new FoodAlias { Id = Guid.NewGuid(), Alias = alias });
        }

        return food;
    }
}