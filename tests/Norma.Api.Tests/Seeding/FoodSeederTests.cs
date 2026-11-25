using Microsoft.EntityFrameworkCore;
using Norma.Api.Data.Entities;
using Norma.Api.Data.Seeding;

namespace Norma.Api.Tests.Seeding;

/// <summary>Загрузка стартового набора в реальную базу: идемпотентность и содержимое.</summary>
[Collection(PostgresCollection.Name)]
public sealed class FoodSeederTests
{
    private readonly PostgresFixture database;

    public FoodSeederTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Seeder_is_idempotent_and_marks_source()
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();

        var first = await FoodSeeder.SeedAsync(db, CancellationToken.None);
        var second = await FoodSeeder.SeedAsync(db, CancellationToken.None);

        Assert.Equal(FoodSeed.Items.Count, first);
        Assert.Equal(0, second);

        var count = await db.Foods.CountAsync(food => food.Source == FoodSource.Seed);
        Assert.Equal(FoodSeed.Items.Count, count);
    }

    [Fact]
    public async Task Seeder_skips_names_that_already_exist()
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();

        // Пользователь уже добавил «Гречка варёная» - второй такой же не нужен.
        var existing = new Food
        {
            Id = Guid.NewGuid(),
            Source = FoodSource.User,
            State = FoodState.Cooked,
            BaseUnit = FoodUnit.Gram,
            KcalPer100g = 110,
            ProteinPer100g = 4.2,
            FatPer100g = 1.1,
            CarbsPer100g = 21.3,
        };
        existing.Names.Add(new FoodName
        {
            Id = Guid.NewGuid(),
            Locale = "ru",
            Name = "гречка варёная",
            IsPrimary = true,
        });

        db.Foods.Add(existing);
        await db.SaveChangesAsync();

        var added = await FoodSeeder.SeedAsync(db, CancellationToken.None);

        Assert.Equal(FoodSeed.Items.Count - 1, added);
        Assert.Equal(1, await db.FoodNames.CountAsync(name => name.Name.ToLower() == "гречка варёная"));
    }
}